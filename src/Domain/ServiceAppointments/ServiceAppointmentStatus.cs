using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AlmaApp.Domain.ServiceAppointments;

public enum ServiceAppointmentStatus
{
    Scheduled = 0,
    Completed = 1,
    Canceled = 9
}