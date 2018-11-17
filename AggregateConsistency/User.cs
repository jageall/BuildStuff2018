using System;
using AggregateConsistency.Infrastructure;

namespace AggregateConsistency
{
    public class User : Aggregate
    {
        private string _id;
        private string _emailAddress;
        private string _pendingEmailAddress;
        private string _name;
        private string _password;
        private string _emailVerificationToken;
        private string _passwordToken;
        private int _failedLoginAttempts;
        private DateTimeOffset _lockoutUntil;
        private User()
        {
        }

        public User(string id, string emailAddress, string name, Func<string> tokenSource)
        {
            Append(new UserCreated(id, emailAddress, name));
            var token = tokenSource();
            Append(new EmailAddressVerificationRequested(id, emailAddress, token));
            Append(new PasswordResetTokenCreated(id, token));
        }

        void Apply(UserCreated @event)
        {
            _id = @event.UserId;
            _name = @event.Name;
        }

        void Apply(EmailAddressVerificationRequested @event)
        {
            _pendingEmailAddress = @event.EmailAddress;
            _emailVerificationToken = @event.Token;
        }

        void Apply(PasswordResetTokenCreated @event)
        {
            _passwordToken = @event.Token;
        }

        public LoginResult Login(string password, Func<string, string> passwordHasher, int maximumLoginAttempts, Func<DateTimeOffset> lockoutPeriod)
        {
            if (!IsVerified) {return LoginResult.Failed;}
            if (_lockoutUntil > Clock.Now) return LoginResult.LockedOut;
            var valid = VerifyPassword(password, passwordHasher);

            if (valid)
            {
                Append(new LoginSucceeded(_id, Clock.Now));
                return LoginResult.Success;
            }
            Append(new LoginFailed(_id, Clock.Now));
            if (_failedLoginAttempts >= maximumLoginAttempts)
            {
                Append(new AccountLockedOut(_id, lockoutPeriod()));
                return LoginResult.LockedOut;
            }
            return LoginResult.Failed;
        }

        void Apply(LoginSucceeded @event)
        {
            _failedLoginAttempts = 0;
            _passwordToken = null;
        }

        void Apply(LoginFailed @event)
        {
            _failedLoginAttempts++;
        }

        void Apply(AccountLockedOut @event)
        {
            _lockoutUntil = @event.LockoutUntil;
        }

        private bool VerifyPassword(string password, Func<string, string> passwordHasher)
        {
            if (_password == null) return false;
            var hashed = passwordHasher(password);
            bool valid = true;
            for (int i = 0; i < hashed.Length; i++)
            {
                valid &= _password[i] == hashed[i];
            }

            return valid;
        }

        public void VerifyEmail(string token)
        {
            if (token != _emailVerificationToken)
                throw new InvalidOperationException("Tokens don't match");
            Append(new EmailAddressVerified(_id, _pendingEmailAddress));
        }

        void Apply(EmailAddressVerified @event)
        {
            _emailVerificationToken = null;
            _emailAddress = _pendingEmailAddress;
        }

        public void SetPassword(string token, string password, Func<string, string> passwordHasher)
        {
            if(_passwordToken != token)
                throw new InvalidOperationException("Tokens don't match");
            Append(new PasswordSet(_id, passwordHasher(password)));
        }

        void Apply(PasswordSet @event)
        {
            _passwordToken = null;
            _password = @event.Password;
        }

        public void ChangeEmail(string newEmailAddress, string password, Func<string> tokenSource, Func<string, string> passwordHasher)
        {
            if (string.Equals(_emailAddress, newEmailAddress, StringComparison.InvariantCulture)) return;
            if(!VerifyPassword(password, passwordHasher)) throw new InvalidOperationException("Password mismatch");
            Append(new EmailAddressVerificationRequested(_id, newEmailAddress, tokenSource()));
        }

        public void ChangePassword(string oldPassword, string newPassword, Func<string, string> passwordHasher)
        {
            if(!VerifyPassword(oldPassword, passwordHasher)) throw new InvalidOperationException();
            Append(new PasswordSet(_id, passwordHasher(newPassword)));
        }

        public void ChangeName(string name)
        {
            if(!string.Equals(_name, name, StringComparison.InvariantCulture)) //TODO: support ui culture?
                Append(new UserNameChanged(_id, name));
        }

        void Apply(UserNameChanged @event)
        {
            ThrowIfNotVerified();
            _name = @event.Name;
        }

        private void ThrowIfNotVerified()
        {
            if (!IsVerified)
                throw new InvalidOperationException("User not verified");
        }

        private bool IsVerified => _emailAddress != null;

        public void RequestPasswordReset(Func<string> tokenSource)
        {
            ThrowIfNotVerified();
            var token = tokenSource();
            Append(new PasswordResetRequested(_id, _emailAddress, token));
            Append(new PasswordResetTokenCreated(_id, token));
        }

        void Apply(PasswordResetRequested @event)
        {
            //no op
        }

        public void LockAccount()
        {
            Append(new AccountLockedOut(_id, DateTimeOffset.MaxValue));
        }

        public void UnlockAccount()
        {
            Append(new AccountUnlocked(_id));
        }

        void Apply(AccountUnlocked @event)
        {
            _lockoutUntil = DateTimeOffset.MinValue;
        }
    }
}