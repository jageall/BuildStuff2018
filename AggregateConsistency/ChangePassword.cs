using System;

namespace AggregateConsistency
{
    public class ChangePassword : UserCommand{
        public ChangePassword(Guid commandId, string userId, string oldPassword, string newPassword) : base(commandId, userId)
        {
            OldPassword = oldPassword;
            NewPassword = newPassword;
        }

        public string OldPassword { get; }
        public string NewPassword { get; }
    }
}