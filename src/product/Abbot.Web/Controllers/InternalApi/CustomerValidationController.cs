using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Models.Api;
using Serious.Abbot.Repositories;

namespace Serious.Abbot.Controllers.InternalApi;

[ApiController]
[AbbotWebHost]
[Route("api/internal/customers")]
public class CustomerValidationController : InternalApiControllerBase
{
    readonly CustomerRepository _repository;

    /// <summary>
    /// Constructs a new instance of the <see cref="CustomerValidationController"/> class.
    /// </summary>
    /// <param name="repository">The <see cref="ITagRepository"/>.</param>
    public CustomerValidationController(CustomerRepository repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// Validates that a name is unique for the customer and organization.
    /// </summary>
    /// <param name="name">Name of the customer to test.</param>
    /// <param name="id">The Id of the current entity.</param>
    /// <returns>A JSON result with true or a string representing the error.</returns>
    [HttpGet("validate")]
    public async Task<IActionResult> ValidateAsync(string name, int id)
    {
        var existing = await _repository.GetCustomerByNameAsync(name, Organization);

        return existing is null || existing.Id == id
            ? Json(true)
            : Json($"A customer with the name \"{name}\" already exists.");
    }

    /// <summary>
    /// Validates that a segment name is unique for the organization.
    /// </summary>
    /// <param name="segmentName">Name of the customer segment to test.</param>
    /// <returns>A JSON result with true or a string representing the error.</returns>
    [HttpGet("segment/validate")]
    public async Task<IActionResult> ValidateSegmentAsync(string segmentName)
    {
        var existing = await _repository.GetCustomerSegmentByNameAsync(segmentName, Organization);

        return existing is null
            ? Json(true)
            : Json($"A customer segment with the name \"{segmentName}\" already exists.");
    }

    /// <summary>
    /// Gets all customer segments used to populate a type-ahead query.
    /// </summary>
    /// <response code="200">A list of segments matching the provided query.</response>
    [HttpGet("segments/typeahead")]
    [ProducesResponseType(typeof(IReadOnlyList<TypeAheadResponseModel>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TypeAheadResponseModel>>> GetSegmentsAsync(
        [FromQuery] string? q,
        [FromQuery] int limit = 10)
    {
        var segments = await _repository.GetCustomerSegmentsForTypeAheadQueryAsync(
            Organization,
            q,
            limit);

        return Ok(segments.Select(TypeAheadResponseModel.Create).ToList());
    }

    /// <summary>
    /// Gets all customers used to populate a type-ahead query.
    /// </summary>
    /// <response code="200">A list of customers matching the provided query.</response>
    [HttpGet("typeahead")]
    [ProducesResponseType(typeof(IReadOnlyList<TypeAheadResponseModel>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TypeAheadResponseModel>>> GetCustomersAsync(
        [FromQuery] string? q,
        [FromQuery] int limit = 10)
    {
        var segments = await _repository.GetCustomersForTypeAheadQueryAsync(
            Organization,
            q,
            limit);

        return Ok(segments.Select(TypeAheadResponseModel.Create).ToList());
    }
}


