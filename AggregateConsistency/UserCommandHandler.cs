using System;

namespace AggregateConsistency
{
    class UserCommandHandler
    {
        private readonly Func<string> _tokenSource;
        private readonly Func<string, string> _passwordHasher;
        private readonly int _maximumLoginAttempts;
        private readonly Func<DateTimeOffset> _lockoutPolicy;

        public UserCommandHandler(
            Func<string> tokenSource,
            Func<string, string> passwordHasher,
            int maximumLoginAttempts,
            Func<DateTimeOffset> lockoutPolicy)
        {
            _tokenSource = tokenSource;
            _passwordHasher = passwordHasher;
            _maximumLoginAttempts = maximumLoginAttempts;
            _lockoutPolicy = lockoutPolicy;
        }

        public User Create(CreateUser cmd)
        {
            return new User(cmd.UserId, cmd.EmailAddress, cmd.Name, _tokenSource);
        }

        public LoginResult Execute(LoginUser cmd, User user)
        {
            return user.Login(cmd.Password, _passwordHasher, _maximumLoginAttempts, _lockoutPolicy);
        }

        public void Execute(VerifyEmailAndSetPassword cmd, User user)
        {
            user.VerifyEmail(cmd.Token);
            user.SetPassword(cmd.Token, cmd.Password, _passwordHasher);
        }

        public void Execute(ChangeEmail cmd, User user)
        {
            user.ChangeEmail(cmd.NewEmailAddress, cmd.Password, _tokenSource, _passwordHasher);
        }

        public void Execute(VerifyEmail cmd, User user)
        {
            user.VerifyEmail(cmd.Token);
        }

        public void Execute(ChangePassword cmd, User user)
        {
            user.ChangePassword(cmd.OldPassword, cmd.NewPassword, _passwordHasher);
        }

        public void Execute(RequestResetPassword cmd, User user)
        {
            user.RequestPasswordReset(_tokenSource);
        }

        public void Execute(SetPassword cmd, User user)
        {
            user.SetPassword(cmd.Token, cmd.Password, _passwordHasher);
        }

        public void Execute(ManualUnlock cmd, User user)
        {
            user.UnlockAccount();
        }

        public void Execute(ManualLock cmd, User user)
        {
            user.LockAccount();
        }

        public void Execute(ChangeName cmd, User user)
        {
            user.ChangeName(cmd.Name);
        }
    }
}