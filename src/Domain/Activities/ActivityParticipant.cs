using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AlmaApp.Domain.Activities
{
    public sealed class ActivityParticipant
    {
        public Guid ActivityId { get; private set; }
        public Guid ClientId { get; private set; }
        public DateTime JoinedAtLocal { get; private set; }
        public DateTime? LeftAtLocal { get; private set; }
        public ActivityParticipantStatus Status { get; private set; }

        private ActivityParticipant() { } // EF

        public ActivityParticipant(Guid activityId, Guid clientId, DateTime joinedAtLocal)
        {
            ActivityId = activityId;
            ClientId = clientId;
            JoinedAtLocal = DateTime.SpecifyKind(joinedAtLocal, DateTimeKind.Unspecified);
            Status = ActivityParticipantStatus.Active;
        }

        public void Cancel(DateTime nowLocal)
        {
            if (Status != ActivityParticipantStatus.Active) return;
            LeftAtLocal = DateTime.SpecifyKind(nowLocal, DateTimeKind.Unspecified);
            Status = ActivityParticipantStatus.Cancelled;
        }
    }
}
