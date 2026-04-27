using Serilog.Core;
using Serilog.Events;

namespace Dok.Api.Logging;

public sealed class PlateDestructuringPolicy : IDestructuringPolicy
{
    public bool TryDestructure(
        object value,
        ILogEventPropertyValueFactory propertyValueFactory,
        out LogEventPropertyValue? result)
    {
        if (value is Plate plate)
        {
            result = new ScalarValue(plate.Masked());
            return true;
        }
        result = null;
        return false;
    }
}
