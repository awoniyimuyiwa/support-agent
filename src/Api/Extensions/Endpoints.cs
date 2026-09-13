using Api.Filters;

namespace Api.Extensions;

public static class Endpoints
{
    extension(RouteHandlerBuilder builder)
    {
        /// <summary>
        /// Adds a validation filter to the endpoint that checks Data Annotations.
        /// </summary>
        public RouteHandlerBuilder WithValidation<T>()
        {
            return builder.AddEndpointFilter<ValidationFilter<T>>()
                .ProducesValidationProblem(); // Adds 400 response to Swagger
        }
    }
}

