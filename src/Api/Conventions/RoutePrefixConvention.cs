using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace Api.Conventions;

public class RoutePrefixConvention(string prefix) : IControllerModelConvention
{
    readonly AttributeRouteModel _routePrefix = new(new Microsoft.AspNetCore.Mvc.RouteAttribute(prefix));

    public void Apply(ControllerModel controller)
    {
        foreach (var selector in controller.Selectors)
        {
            selector.AttributeRouteModel = selector.AttributeRouteModel != null
                ? AttributeRouteModel.CombineAttributeRouteModel(_routePrefix, selector.AttributeRouteModel)
                : _routePrefix;
        }
    }
}