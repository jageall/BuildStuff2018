using System;
using AggregateConsistency.Infrastructure;

namespace AggregateConsistency
{
    public static class UserBoundedContext
    {
        public static ISerializationRegistry RegisterSerializers(ISerializationRegistry registry, IKeyStore store)
        {
            registry
                .WithEncryption(store)
                .DefaultForEvent<UserCreated>()
                .DefaultForEvent<EmailAddressVerificationRequested>()
                .DefaultForEvent<PasswordResetTokenCreated>()
                .DefaultForEvent<EmailAddressVerified>()
                .DefaultForEvent<PasswordResetRequested>()
                .DefaultForEvent<UserNameChanged>()
                .DefaultForEvent<PasswordSet>();
            registry
                .DefaultForEvent<LoginFailed>()
                .DefaultForEvent<LoginSucceeded>()
                .DefaultForEvent<AccountLockedOut>()
                .DefaultForEvent<AccountUnlocked>();
            return registry;
        }

        public static ICommandRegistry RegisterCommands(
            ICommandRegistry registry, 
            UserRepository repository, 
            Func<string> tokenSource, 
            Func<string, string> passwordHasher,
            int maxLoginAttempts,
            Func<DateTimeOffset> lockoutPolicy)
        {
            var handler = new UserCommandHandler(tokenSource, passwordHasher, maxLoginAttempts, lockoutPolicy);
            var userRegistry = registry.For<User>();
            userRegistry
                .WithDefaults(repository.SaveNew, id => repository.Load(id),
                    repository.Save)
                .Create<CreateUser>(handler.Create)
                .Execute<VerifyEmailAndSetPassword>(handler.Execute)
                .Execute<ChangeEmail>(handler.Execute)
                .Execute<VerifyEmail>(handler.Execute)
                .Execute<ChangePassword>(handler.Execute)
                .Execute<ChangeName>(handler.Execute)
                .Execute<RequestResetPassword>(handler.Execute)
                .Execute<SetPassword>(handler.Execute);
            userRegistry
                .WithDefaults(
                    (_, __) => throw new NotSupportedException(), id => repository.Load(id, true), repository.Save)
                .Execute<LoginUser, LoginResult>(handler.Execute)
                .Execute<ManualUnlock>(handler.Execute)
                .Execute<ManualLock>(handler.Execute);


            return registry;
        }
    }
}