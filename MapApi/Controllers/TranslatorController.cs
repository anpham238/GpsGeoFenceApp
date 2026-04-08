using MapApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace MapApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TranslatorController : ControllerBase
{
    private readonly TranslatorClient _translator;

    public TranslatorController(TranslatorClient translator)
    {
        _translator = translator;
    }

    [HttpPost("translate")]
    public async Task<ActionResult<string>> Translate(
        [FromBody] TranslateRequest req,
        CancellationToken ct)
    {
        var result = await _translator.TryTranslateAsync(req.Text, req.ToLang, req.FromLang, ct);
        return Ok(result ?? req.Text);
    }
}
public record TranslateRequest(string Text, string ToLang, string? FromLang = null);
