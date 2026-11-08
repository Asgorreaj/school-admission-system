using Microsoft.AspNetCore.Mvc; // controller base
using SchoolAdmission.Api.Models; // DTO
using SchoolAdmission.Api.Services; // service

namespace SchoolAdmission.Api.Controllers; // controllers namespace

[ApiController] // auto 400 for bad JSON
public class ClassesController(AdmissionService svc) : ControllerBase // class endpoints
{
    [HttpPost("/classes")] // POST /classes
    public async Task<IActionResult> Create(CreateClassRequest req) { var d = await svc.CreateClassAsync(req); return Created($"/classes/{d.Id}", d); } // 201

    [HttpGet("/classes")] // GET /classes
    public async Task<IActionResult> List() => Ok(await svc.ListClassesAsync()); // list

    [HttpGet("/classes/{classId}")] // GET /classes/{id}
    public async Task<IActionResult> Get(string classId) => Ok(await svc.GetClassAsync(classId)); // details

    [HttpPost("/classes/{classId}/holds")] // POST /classes/{id}/holds
    public async Task<IActionResult> CreateHold(string classId, [FromHeader(Name = "X-Student-Id")] string? studentId) // abedon
    { var d = await svc.CreateHoldAsync(classId, studentId); return Created($"/holds/{d.HoldId}", d); } // 201
}

[ApiController] // auto validation
public class HoldsController(AdmissionService svc) : ControllerBase // hold endpoints
{
    [HttpGet("/holds/{holdId}")] // GET /holds/{id}
    public async Task<IActionResult> Get(string holdId) => Ok(await svc.GetHoldAsync(holdId)); // details

    [HttpPost("/holds/{holdId}/confirm")] // POST confirm
    public async Task<IActionResult> Confirm(string holdId, [FromHeader(Name = "X-Student-Id")] string? studentId) => Ok(await svc.ConfirmHoldAsync(holdId, studentId)); // confirm

    [HttpDelete("/holds/{holdId}")] // DELETE hold
    public async Task<IActionResult> Cancel(string holdId, [FromHeader(Name = "X-Student-Id")] string? studentId) { await svc.CancelHoldAsync(holdId, studentId); return NoContent(); } // 204

    [HttpGet("/me/holds")] // GET /me/holds
    public async Task<IActionResult> Mine([FromHeader(Name = "X-Student-Id")] string? studentId) => Ok(await svc.ListMyHoldsAsync(studentId)); // nijer hold
}