using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AlmaApp.Domain.GroupClasses
{
    public sealed class GroupClassParticipant
    {
        public Guid GroupClassId { get; private set; }
        public Guid ClientId { get; private set; }
        public DateTime JoinedAtLocal { get; private set; }
        public DateTime? LeftAtLocal { get; private set; }
        public GroupClassParticipantStatus Status { get; private set; }

        private GroupClassParticipant() { } // EF

        public GroupClassParticipant(Guid groupClassId, Guid clientId, DateTime joinedAtLocal)
        {
            GroupClassId = groupClassId;
            ClientId = clientId;
            JoinedAtLocal = DateTime.SpecifyKind(joinedAtLocal, DateTimeKind.Unspecified);
            Status = GroupClassParticipantStatus.Active;
        }

        public void Cancel(DateTime nowLocal)
        {
            if (Status != GroupClassParticipantStatus.Active) return;
            LeftAtLocal = DateTime.SpecifyKind(nowLocal, DateTimeKind.Unspecified);
            Status = GroupClassParticipantStatus.Cancelled;
        }

        public void MarkNoShow(DateTime nowLocal)
        {
            if (Status != GroupClassParticipantStatus.Active) return;
            LeftAtLocal = DateTime.SpecifyKind(nowLocal, DateTimeKind.Unspecified);
            Status = GroupClassParticipantStatus.NoShow;
        }
    }
}
