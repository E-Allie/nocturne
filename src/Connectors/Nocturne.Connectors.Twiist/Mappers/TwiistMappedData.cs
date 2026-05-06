using Nocturne.Core.Models;
using Nocturne.Core.Models.V4;

namespace Nocturne.Connectors.Twiist.Mappers;

public sealed class TwiistMappedData
{
    public List<SensorGlucose> SensorGlucose { get; } = [];
    public List<Bolus> Boluses { get; } = [];
    public List<CarbIntake> CarbIntakes { get; } = [];
    public List<TempBasal> TempBasals { get; } = [];
    public List<DeviceEvent> DeviceEvents { get; } = [];
    public List<Note> Notes { get; } = [];
    public List<DeviceStatus> DeviceStatuses { get; } = [];
}
