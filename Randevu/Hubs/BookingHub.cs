using Microsoft.AspNetCore.SignalR;

namespace Randevu.Hubs;

public class BookingHub : Hub
{
    // İstemci, salon ve aktif hafta (mondayIso) için gruba join olur.
    // Böylece sadece ilgili salon+hafta değişiklikleri broadcast edilir.
    public Task JoinWeekGroup(int salonId, string mondayIso)
        => Groups.AddToGroupAsync(Context.ConnectionId, $"salon:{salonId}:week:{mondayIso}");

    public Task LeaveWeekGroup(int salonId, string mondayIso)
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, $"salon:{salonId}:week:{mondayIso}");
}
