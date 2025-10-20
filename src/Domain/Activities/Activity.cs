using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using AlmaApp.Domain.Activities;

namespace AlmaApp.Domain.Activities
{
    public enum ActivityStatus
    {
        Scheduled = 0,
        Completed = 1,
        Canceled = 2
    }

    public sealed class Activity
    {
        public Guid Id { get; private set; }
        public Guid RoomId { get; private set; }
        public Guid InstructorId { get; private set; }  // Novo
        public string Title { get; private set; } = default!;
        public string? Description { get; private set; }
        public ActivityCategory Category { get; private set; } // Ex.: Workshop
        public DateTime StartLocal { get; private set; }
        public int DurationMinutes { get; private set; }
        public int MaxParticipants { get; private set; }       // Novo
        public ActivityStatus Status { get; private set; }
        public string? CreatedByUid { get; private set; }
        public DateTime CreatedAtLocal { get; private set; }
        [Timestamp] public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

        private readonly List<ActivityParticipant> _participants = new();
        public IReadOnlyCollection<ActivityParticipant> Participants => _participants.AsReadOnly();

        private Activity() { } // EF

        public Activity(Guid roomId, Guid instructorId, string title, string? description,
                        ActivityCategory category, DateTime startLocal, int durationMinutes, int maxParticipants,
                        string? createdByUid, DateTime createdAtLocal)
        {
            if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException(nameof(title));
            if (durationMinutes <= 0) throw new ArgumentOutOfRangeException(nameof(durationMinutes));
            if (maxParticipants <= 0) throw new ArgumentOutOfRangeException(nameof(maxParticipants));

            Id = Guid.NewGuid();
            RoomId = roomId;
            InstructorId = instructorId;
            Title = title.Trim();
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
            Category = category;
            StartLocal = DateTime.SpecifyKind(startLocal, DateTimeKind.Unspecified);
            DurationMinutes = durationMinutes;
            MaxParticipants = maxParticipants;
            Status = ActivityStatus.Scheduled;
            CreatedByUid = createdByUid;
            CreatedAtLocal = DateTime.SpecifyKind(createdAtLocal, DateTimeKind.Unspecified);
        }

        public DateTime EndLocal => StartLocal.AddMinutes(DurationMinutes);
        public int ActiveCount => _participants.Count(p => p.Status == ActivityParticipantStatus.Active);
        public int AvailableSlots => Math.Max(0, MaxParticipants - ActiveCount);
        public bool IsFull => AvailableSlots <= 0;

        public void Update(Guid roomId, Guid instructorId, string title, string? description,
                           ActivityCategory category, DateTime startLocal, int durationMinutes, int maxParticipants)
        {
            if (Status != ActivityStatus.Scheduled)
                throw new InvalidOperationException("Só é possível editar actividades agendadas.");
            if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException(nameof(title));
            if (durationMinutes <= 0) throw new ArgumentOutOfRangeException(nameof(durationMinutes));
            if (maxParticipants <= 0) throw new ArgumentOutOfRangeException(nameof(maxParticipants));
            if (ActiveCount > maxParticipants)
                throw new InvalidOperationException("Capacidade inferior ao número de participantes activos.");

            RoomId = roomId;
            InstructorId = instructorId;
            Title = title.Trim();
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
            Category = category;
            StartLocal = DateTime.SpecifyKind(startLocal, DateTimeKind.Unspecified);
            DurationMinutes = durationMinutes;
            MaxParticipants = maxParticipants;
        }

        public void Cancel()
        {
            if (Status == ActivityStatus.Canceled) return;
            if (Status == ActivityStatus.Completed)
                throw new InvalidOperationException("Actividade já concluída.");
            Status = ActivityStatus.Canceled;
        }

        public void Complete()
        {
            if (Status != ActivityStatus.Scheduled)
                throw new InvalidOperationException("Só é possível concluir actividades agendadas.");
            Status = ActivityStatus.Completed;
        }

        public void AddParticipant(Guid clientId, DateTime nowLocal)
        {
            if (Status != ActivityStatus.Scheduled)
                throw new InvalidOperationException("Actividade não está agendada.");
            if (IsFull) throw new InvalidOperationException("Actividade sem vagas.");
            if (_participants.Any(p => p.ClientId == clientId && p.Status == ActivityParticipantStatus.Active))
                throw new InvalidOperationException("Cliente já inscrito.");
            _participants.Add(new ActivityParticipant(Id, clientId, nowLocal));
        }

        public void RemoveParticipant(Guid clientId, DateTime nowLocal)
        {
            var p = _participants.FirstOrDefault(x => x.ClientId == clientId && x.Status == ActivityParticipantStatus.Active);
            if (p == null) return;
            p.Cancel(nowLocal);
        }
    }
}
