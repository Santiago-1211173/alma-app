using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;

namespace AlmaApp.Domain.GroupClasses
{
    public sealed class GroupClass
    {
        public Guid Id { get; private set; }
        public Guid InstructorId { get; private set; }
        public Guid RoomId { get; private set; }
        public GroupClassCategory Category { get; private set; }
        public string? Title { get; private set; }
        public DateTime StartLocal { get; private set; } // Europe/Lisbon, sem Z
        public int DurationMinutes { get; private set; }
        public int MaxParticipants { get; private set; }
        public GroupClassStatus Status { get; private set; }

        public string? CreatedByUid { get; private set; }
        public DateTime CreatedAtLocal { get; private set; }

        [Timestamp] public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

        private readonly List<GroupClassParticipant> _participants = new();
        public IReadOnlyCollection<GroupClassParticipant> Participants => _participants.AsReadOnly();

        private GroupClass() { } // EF

        public GroupClass(Guid instructorId, Guid roomId, GroupClassCategory category, string? title,
                          DateTime startLocal, int durationMinutes, int maxParticipants,
                          string? createdByUid, DateTime createdAtLocal)
        {
            if (durationMinutes <= 0) throw new ArgumentOutOfRangeException(nameof(durationMinutes));
            if (maxParticipants <= 0) throw new ArgumentOutOfRangeException(nameof(maxParticipants));

            Id = Guid.NewGuid();
            InstructorId = instructorId;
            RoomId = roomId;
            Category = category;
            Title = string.IsNullOrWhiteSpace(title) ? null : title.Trim();
            StartLocal = DateTime.SpecifyKind(startLocal, DateTimeKind.Unspecified);
            DurationMinutes = durationMinutes;
            MaxParticipants = maxParticipants;
            Status = GroupClassStatus.Scheduled;
            CreatedByUid = createdByUid;
            CreatedAtLocal = DateTime.SpecifyKind(createdAtLocal, DateTimeKind.Unspecified);
        }

        public DateTime EndLocal => StartLocal.AddMinutes(DurationMinutes);
        public int ActiveCount => _participants.Count(p => p.Status == GroupClassParticipantStatus.Active);
        public int AvailableSlots => Math.Max(0, MaxParticipants - ActiveCount);
        public bool IsFull => AvailableSlots <= 0;

        public void Update(Guid instructorId, Guid roomId, GroupClassCategory category, string? title,
                           DateTime startLocal, int durationMinutes, int maxParticipants)
        {
            if (Status != GroupClassStatus.Scheduled)
                throw new InvalidOperationException("Só é possível editar aulas agendadas.");

            if (durationMinutes <= 0) throw new ArgumentOutOfRangeException(nameof(durationMinutes));
            if (maxParticipants <= 0) throw new ArgumentOutOfRangeException(nameof(maxParticipants));
            if (ActiveCount > maxParticipants) throw new InvalidOperationException("Capacidade inferior ao número de inscritos activos.");

            InstructorId = instructorId;
            RoomId = roomId;
            Category = category;
            Title = string.IsNullOrWhiteSpace(title) ? null : title.Trim();
            StartLocal = DateTime.SpecifyKind(startLocal, DateTimeKind.Unspecified);
            DurationMinutes = durationMinutes;
            MaxParticipants = maxParticipants;
        }

        public void Cancel()
        {
            if (Status == GroupClassStatus.Canceled) return;
            if (Status == GroupClassStatus.Completed)
                throw new InvalidOperationException("Aula já concluída.");
            Status = GroupClassStatus.Canceled;
        }

        public void Complete()
        {
            if (Status != GroupClassStatus.Scheduled)
                throw new InvalidOperationException("Só é possível concluir aulas agendadas.");
            Status = GroupClassStatus.Completed;
        }

        public void AddParticipant(Guid clientId, DateTime nowLocal)
        {
            if (Status != GroupClassStatus.Scheduled)
                throw new InvalidOperationException("Aula não está agendada.");
            if (IsFull) throw new InvalidOperationException("Aula sem vagas.");
            if (_participants.Any(p => p.ClientId == clientId && p.Status == GroupClassParticipantStatus.Active))
                throw new InvalidOperationException("Cliente já inscrito.");

            _participants.Add(new GroupClassParticipant(Id, clientId, DateTime.SpecifyKind(nowLocal, DateTimeKind.Unspecified)));
        }

        public void RemoveParticipant(Guid clientId, DateTime nowLocal)
        {
            var p = _participants.FirstOrDefault(x => x.ClientId == clientId && x.Status == GroupClassParticipantStatus.Active);
            if (p == null) return;
            p.Cancel(nowLocal);
        }
    }
}
