using AggregateConsistency;
using AggregateConsistency.Infrastructure;
using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace AggregateConsistencyTests
{
    public class UserUseCases
    {
        private readonly TestEventStore _eventStore;
        private readonly string _token;
        private readonly ICommandHandler _handler;
        private readonly Func<string, string> _passwordHasher;
        private readonly IKeyStore _keyStore;
        private DateTimeOffset _now;
        protected internal Func<DateTimeOffset> _lockoutPolicy;

        public UserUseCases()
        {
            _token = Guid.NewGuid().ToString("N");
            _passwordHasher = s => Convert.ToBase64String(MD5.Create().ComputeHash(Encoding.ASCII.GetBytes(s)));
            _keyStore = new ReallyInsecureAndVeryTemporaryKeyStore();
            _keyStore.CreateKeyIfNotExists("test");
            SerializationRegistry serializationRegistry = new SerializationRegistry();
            UserBoundedContext.RegisterSerializers(serializationRegistry, _keyStore);
            IKnownSerializers serialization = serializationRegistry.Build();
            _eventStore = new TestEventStore(serialization);
            CommandRegistry commandRegistry = new CommandRegistry();
            _lockoutPolicy = () => Clock.Now + TimeSpan.FromMinutes(5);
            UserBoundedContext.RegisterCommands(
                commandRegistry,
                new UserRepository(_eventStore, serialization),
                tokenSource: () => _token,
                passwordHasher: _passwordHasher,
                maxLoginAttempts: 3,
                lockoutPolicy: _lockoutPolicy);
            _handler = commandRegistry.Build();
            _now = DateTimeOffset.UtcNow;
            Clock.UtcNowFunc = () => _now;
        }
        [Fact]
        public async Task CanCreateUser()
        {
            string userId = "test";
            string emailAddress = "someone@example.com";
            string name = "John Doe";
            await _handler.Execute(new CreateUser(Guid.NewGuid(), userId, emailAddress, name));
            _eventStore.AssertEvents(userId, "user-test",
                new UserCreated(userId, emailAddress, name),
                new EmailAddressVerificationRequested(userId, emailAddress, _token),
                new PasswordResetTokenCreated(userId, _token)
                );
        }

        [Fact]
        public async Task UserCanVerifyEmailAndSetPassword()
        {
            string userId = "test";
            string emailAddress = "someone@example.com";
            string name = "John Doe";
            _eventStore.WithEvents(userId, "user-test",
                new UserCreated(userId, emailAddress, name),
                new EmailAddressVerificationRequested(userId, emailAddress, _token),
                new PasswordResetTokenCreated(userId, _token)
            );

            string password = "password";
            await _handler.Execute(new VerifyEmailAndSetPassword(Guid.NewGuid(), userId, _token, password));
            _eventStore.AssertEvents(userId, "user-test",
                new EmailAddressVerified(userId, emailAddress),
                new PasswordSet(userId, _passwordHasher(password))
                );
        }

        [Fact]
        public async Task VerifyingWithTheWrongTokenShouldThrow()
        {
            string userId = "test";
            string emailAddress = "someone@example.com";
            string name = "John Doe";
            _eventStore.WithEvents(userId, "user-test",
                new UserCreated(userId, emailAddress, name),
                new EmailAddressVerificationRequested(userId, emailAddress, _token),
                new PasswordResetTokenCreated(userId, _token)
            );

            Exception ex = await Record.ExceptionAsync(
                () => _handler.Execute(new VerifyEmailAndSetPassword(Guid.NewGuid(), userId, "a wrong token",
                    "does not matter")));
            Assert.IsAssignableFrom<Exception>(ex);
        }

        [Fact]
        public async Task AnUnverifiedEmailAddressCannotLogin()
        {
            string userId = "test";
            string emailAddress = "someone@example.com";
            string name = "John Doe";
            _eventStore.WithEvents("test", "user-test",
                new UserCreated(userId, emailAddress, name),
                new EmailAddressVerificationRequested(userId, emailAddress, _token),
                new PasswordResetTokenCreated(userId, _token)
            );

            Guid commandId = Guid.NewGuid();
            Result result = await _handler.Execute(new LoginUser(commandId, userId, "password"));
            Assert.Equal(LoginResult.Failed, result.Value<LoginResult>(commandId));
            //Before a user can login, there account shouldn't be locked out.
            Assert.False(_eventStore.Streams.ContainsKey("userLogin-test"));
        }

        [Fact]
        public async Task VerifiedUserCanLogin()
        {
            string userId = "test";
            string emailAddress = "someone@example.com";
            string name = "John Doe";
            string password = "password";
            _eventStore.WithEvents("test", "user-test",
                new UserCreated(userId, emailAddress, name),
                new EmailAddressVerificationRequested(userId, emailAddress, _token),
                new PasswordResetTokenCreated(userId, _token),
                new EmailAddressVerified(userId, emailAddress),
                new PasswordSet(userId, _passwordHasher(password))
            );
            Guid commandId = Guid.NewGuid();
            Result result = await _handler.Execute(new LoginUser(commandId, userId, password));
            Assert.Equal(LoginResult.Success, result.Value<LoginResult>(commandId));
            _eventStore.AssertEvents(userId, "userLogin-test", new LoginSucceeded(userId, _now));
        }

        [Fact]
        public async Task UsersAreLockedOutAccordingToAfterMaxLoginAttemptsFail()
        {
            string userId = "test";
            string emailAddress = "someone@example.com";
            string name = "John Doe";
            string password = "password";
            _eventStore.WithEvents("test", "user-test",
                new UserCreated(userId, emailAddress, name),
                new EmailAddressVerificationRequested(userId, emailAddress, _token),
                new PasswordResetTokenCreated(userId, _token),
                new EmailAddressVerified(userId, emailAddress),
                new PasswordSet(userId, _passwordHasher(password))
            );
            LoginFailed failedLogin = new LoginFailed(userId, _now);
            failedLogin.AddMetadataValue("userVersion", _eventStore.Streams["user-test"].Count - 1);
            _eventStore.WithEvents(userId, "userLogin-test", failedLogin, failedLogin);
            Guid commandId = Guid.NewGuid();
            Result result = await _handler.Execute(new LoginUser(commandId, userId, "not the password"));
            Assert.Equal(LoginResult.LockedOut, result.Value<LoginResult>(commandId));
            _eventStore.AssertEvents(userId, "userLogin-test",
                new LoginFailed(userId, _now),
                new AccountLockedOut(userId, _lockoutPolicy()));
        }

        [Fact]
        public async Task WhenEnoughTimeHasPassedLockedOutUserCanLogin()
        {
            string userId = "test";
            string emailAddress = "someone@example.com";
            string name = "John Doe";
            string password = "password";
            _eventStore.WithEvents("test", "user-test",
                new UserCreated(userId, emailAddress, name),
                new EmailAddressVerificationRequested(userId, emailAddress, _token),
                new PasswordResetTokenCreated(userId, _token),
                new EmailAddressVerified(userId, emailAddress),
                new PasswordSet(userId, _passwordHasher(password)),
                new LoginFailed(userId, _now),
                new LoginFailed(userId, _now),
                new LoginFailed(userId, _now),
                new AccountLockedOut(userId, _lockoutPolicy())
            );
            _now = _lockoutPolicy();
            Guid commandId = Guid.NewGuid();
            Result result = await _handler.Execute(new LoginUser(commandId, userId, password));
            Assert.Equal(LoginResult.Success, result.Value<LoginResult>(commandId));

            _eventStore.AssertEvents(userId, "userLogin-test", new LoginSucceeded(userId, _now));
        }

        [Fact]
        public async Task UserCanRequestPasswordReset()
        {
            string userId = "test";
            string emailAddress = "someone@example.com";
            string name = "John Doe";
            string password = "password";
            _eventStore.WithEvents("test", "user-test",
                new UserCreated(userId, emailAddress, name),
                new EmailAddressVerificationRequested(userId, emailAddress, _token),
                new PasswordResetTokenCreated(userId, _token),
                new EmailAddressVerified(userId, emailAddress),
                new PasswordSet(userId, _passwordHasher(password)));
            await _handler.Execute(new RequestResetPassword(Guid.NewGuid(), userId));

            _eventStore.AssertEvents(userId, "user-test",
                new PasswordResetRequested(userId, emailAddress, _token),
                new PasswordResetTokenCreated(userId, _token));
        }

        [Fact]
        public async Task UserCanResetPassword()
        {
            string userId = "test";
            string emailAddress = "someone@example.com";
            string name = "John Doe";
            string password = "password";
            _eventStore.WithEvents("test", "user-test",
                new UserCreated(userId, emailAddress, name),
                new EmailAddressVerificationRequested(userId, emailAddress, _token),
                new PasswordResetTokenCreated(userId, _token),
                new EmailAddressVerified(userId, emailAddress),
                new PasswordSet(userId, _passwordHasher(password)),
                new PasswordResetRequested(userId, emailAddress, _token),
                new PasswordResetTokenCreated(userId, _token));
            string password2 = "password2";
            await _handler.Execute(new SetPassword(Guid.NewGuid(), userId, _token, password2));

            _eventStore.AssertEvents(userId, "user-test",
                new PasswordSet(userId, _passwordHasher(password2))
            );
        }

        [Fact]
        public async Task UsingTheWrongTokenToSetThePassword()
        {
            string userId = "test";
            string emailAddress = "someone@example.com";
            string name = "John Doe";
            string password = "password";
            _eventStore.WithEvents("test", "user-test",
                new UserCreated(userId, emailAddress, name),
                new EmailAddressVerificationRequested(userId, emailAddress, _token),
                new PasswordResetTokenCreated(userId, _token),
                new EmailAddressVerified(userId, emailAddress),
                new PasswordSet(userId, _passwordHasher(password)),
                new PasswordResetRequested(userId, emailAddress, _token),
                new PasswordResetTokenCreated(userId, _token));
            Exception ex = await Record.ExceptionAsync(() => _handler.Execute(new SetPassword(Guid.NewGuid(), userId, "the wrong token", "does not matter")));
            Assert.IsAssignableFrom<Exception>(ex);
        }

        [Fact]
        public async Task VerifiedUsersCanChangeName()
        {
            string userId = "test";
            string emailAddress = "someone@example.com";
            string name = "John Doe";
            string password = "password";
            _eventStore.WithEvents("test", "user-test",
                new UserCreated(userId, emailAddress, name),
                new EmailAddressVerificationRequested(userId, emailAddress, _token),
                new PasswordResetTokenCreated(userId, _token),
                new EmailAddressVerified(userId, emailAddress),
                new PasswordSet(userId, _passwordHasher(password)));

            string name2 = "Jane Doe";
            await _handler.Execute(new ChangeName(Guid.NewGuid(), userId, name2));

            _eventStore.AssertEvents(userId, "user-test", new UserNameChanged(userId, name2));
        }

        [Fact]
        public async Task UnverifiedUsersCannotChangeName()
        {
            string userId = "test";
            string emailAddress = "someone@example.com";
            string name = "John Doe";
            _eventStore.WithEvents("test", "user-test",
                new UserCreated(userId, emailAddress, name),
                new EmailAddressVerificationRequested(userId, emailAddress, _token),
                new PasswordResetTokenCreated(userId, _token));

            string name2 = "Jane Doe";
            Exception ex = await Record.ExceptionAsync(() => _handler.Execute(new ChangeName(Guid.NewGuid(), userId, name2)));

            Assert.IsAssignableFrom<Exception>(ex);
        }

        [Fact]
        public async Task UserCanChangeEmailAddress()
        {
            string userId = "test";
            string emailAddress = "someone@example.com";
            string name = "John Doe";
            string password = "password";
            _eventStore.WithEvents("test", "user-test",
                new UserCreated(userId, emailAddress, name),
                new EmailAddressVerificationRequested(userId, emailAddress, _token),
                new PasswordResetTokenCreated(userId, _token),
                new EmailAddressVerified(userId, emailAddress),
                new PasswordSet(userId, _passwordHasher(password)));
            string newEmailAddress = "someoneelse@example.com";
            await _handler.Execute(new ChangeEmail(Guid.NewGuid(), userId, newEmailAddress, password));
            _eventStore.AssertEvents(userId, "user-test", new EmailAddressVerificationRequested(userId, newEmailAddress, _token));
        }

        [Fact]
        public async Task UserCanChangeVerifyNewEmailAddress()
        {
            string userId = "test";
            string emailAddress = "someone@example.com";
            string newEmailAddress = "someoneelse@example.com";
            string name = "John Doe";
            string password = "password";
            _eventStore.WithEvents("test", "user-test",
                new UserCreated(userId, emailAddress, name),
                new EmailAddressVerificationRequested(userId, emailAddress, _token),
                new PasswordResetTokenCreated(userId, _token),
                new EmailAddressVerified(userId, emailAddress),
                new PasswordSet(userId, _passwordHasher(password)),
                new EmailAddressVerificationRequested(userId, newEmailAddress, _token));

            await _handler.Execute(new VerifyEmail(Guid.NewGuid(), userId, _token));

            _eventStore.AssertEvents(userId, "user-test", new EmailAddressVerified(userId, newEmailAddress));
        }

        [Fact]
        public async Task UserCanChangePassword()
        {
            string userId = "test";
            string emailAddress = "someone@example.com";
            string name = "John Doe";
            string password = "password";
            _eventStore.WithEvents("test", "user-test",
                new UserCreated(userId, emailAddress, name),
                new EmailAddressVerificationRequested(userId, emailAddress, _token),
                new PasswordResetTokenCreated(userId, _token),
                new EmailAddressVerified(userId, emailAddress),
                new PasswordSet(userId, _passwordHasher(password)));
            string newPassword = "Password2";
            await _handler.Execute(new ChangePassword(Guid.NewGuid(), userId, password, newPassword));
            _eventStore.AssertEvents(userId, "user-test", new PasswordSet(userId, _passwordHasher(newPassword)));
        }

        [Fact]
        public async Task ManualLockShouldImmediatelyLockUserAccount()
        {
            string userId = "test";
            string emailAddress = "someone@example.com";
            string name = "John Doe";
            string password = "password";
            _eventStore.WithEvents("test", "user-test",
                new UserCreated(userId, emailAddress, name),
                new EmailAddressVerificationRequested(userId, emailAddress, _token),
                new PasswordResetTokenCreated(userId, _token),
                new EmailAddressVerified(userId, emailAddress),
                new PasswordSet(userId, _passwordHasher(password)));

            await _handler.Execute(new ManualLock(Guid.NewGuid(), userId));

            _eventStore.AssertEvents(userId, "userLogin-test", new AccountLockedOut(userId, DateTimeOffset.MaxValue));
        }

        [Fact]
        public async Task ManualUnlockShouldImmediatelyUnlockUserAccount()
        {
            string userId = "test";
            string emailAddress = "someone@example.com";
            string name = "John Doe";
            string password = "password";
            _eventStore.WithEvents("test", "user-test",
                new UserCreated(userId, emailAddress, name),
                new EmailAddressVerificationRequested(userId, emailAddress, _token),
                new PasswordResetTokenCreated(userId, _token),
                new EmailAddressVerified(userId, emailAddress),
                new PasswordSet(userId, _passwordHasher(password)));
            AccountLockedOut accountLockedOut = new AccountLockedOut(userId, DateTimeOffset.MaxValue);
            accountLockedOut.AddMetadataValue("userVersion", _eventStore.Streams["user-test"].Count - 1);
            _eventStore.WithEvents(userId, "userLogin-test", accountLockedOut);
            await _handler.Execute(new ManualUnlock(Guid.NewGuid(), userId));

            _eventStore.AssertEvents(userId, "userLogin-test", new AccountUnlocked(userId));
        }

        [Fact]
        public async Task KeyShreddedUserShouldNotBeAbleToLogin()
        {
            string userId = "test";
            string emailAddress = "someone@example.com";
            string name = "John Doe";
            string password = "password";

            _eventStore.WithEvents("test", "user-test",
                new UserCreated(userId, emailAddress, name),
                new EmailAddressVerificationRequested(userId, emailAddress, _token),
                new PasswordResetTokenCreated(userId, _token),
                new EmailAddressVerified(userId, emailAddress),
                new PasswordSet(userId, _passwordHasher(password))
            );
            _keyStore.Destroy("test");
            var commandId = Guid.NewGuid();
            var result = await _handler.Execute(new LoginUser(commandId, userId, password));
            Assert.Equal(LoginResult.Failed, result.Value<LoginResult>(commandId));
            Assert.False(_eventStore.Streams.ContainsKey("userLogin-test"));
            _eventStore.AssertEvents("test", "user-test");
        }
    }
}
