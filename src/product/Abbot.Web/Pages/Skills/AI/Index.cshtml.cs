using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using OpenAI_API.Models;
using Serious.Abbot.AI;
using Serious.Abbot.FeatureManagement;
using Serious.Abbot.Repositories;
using Serious.Abbot.Web;

namespace Serious.Abbot.Pages.Skills.AI;

[PageFeatureGate(FeatureFlags.AISkillPrompts)]
public class IndexPageModel : SkillFeatureEditPageModel
{
    public static readonly DomId TestExtractorResultId = new("test-extractor-result");

    readonly ISkillRepository _skillRepository;
    readonly ArgumentRecognizer _recognizer;
    readonly IOpenAIClient _openAIClient;

    public int? EditingExemplarId { get; set; }

    [BindProperty]
    public string? Exemplar { get; set; }

    [BindProperty]
    public string? ExpectedArguments { get; set; }

    [BindProperty]
    public string? TestMessage { get; set; }

    public IndexPageModel(
        ISkillRepository skillRepository,
        IOpenAIClient openAIClient,
        ArgumentRecognizer recognizer)
    {
        _skillRepository = skillRepository;
        _openAIClient = openAIClient;
        _recognizer = recognizer;
    }

    public async Task<IActionResult> OnGetAsync(string skill)
    {
        var currentMember = Viewer;
        var dbSkill = await _skillRepository.GetAsync(skill, currentMember.Organization);
        if (dbSkill is null)
        {
            return NotFound();
        }

        Skill = dbSkill;

        return Page();
    }

    public async Task<IActionResult> OnGetEditAsync(string skill, int exemplarId)
    {
        var currentMember = Viewer;
        var dbSkill = await _skillRepository.GetAsync(skill, currentMember.Organization);
        if (dbSkill is null)
        {
            return NotFound();
        }

        Skill = dbSkill;

        var exemplar = Skill.Exemplars.SingleOrDefault(p => p.Id == exemplarId);
        if (exemplar is null)
        {
            return NotFound();
        }

        EditingExemplarId = exemplarId;
        Exemplar = exemplar.Exemplar;
        ExpectedArguments = exemplar.Properties.Arguments;
        return Page();
    }

    public async Task<IActionResult> OnPostEditAsync(string skill, int? exemplarId)
    {
        var currentMember = Viewer;
        var dbSkill = await _skillRepository.GetAsync(skill, currentMember.Organization);
        if (dbSkill is null)
        {
            return NotFound();
        }

        Skill = dbSkill;

        if (exemplarId is null)
        {
            return BadRequest();
        }

        var exemplar = Skill.Exemplars.SingleOrDefault(p => p.Id == exemplarId.Value);
        if (exemplar is null)
        {
            return NotFound();
        }

        // Compute embeddings for the prompt
        var embeddings =
            await _openAIClient.CreateEmbeddingsAsync(exemplar.Exemplar, Model.AdaTextEmbedding, Organization);

        await _skillRepository.UpdateExemplarAsync(
            exemplar,
            Exemplar.Require(),
            new()
            {
                Arguments = ExpectedArguments,
                EmbeddingVector = embeddings.Data.Single().Embedding.Select(f => (double)f).ToArray(),
            },
            Viewer);

        return RedirectToPage(null,
            new {
                skill
            });
    }

    public async Task<IActionResult> OnPostDeleteAsync(string skill, int? exemplarId)
    {
        var currentMember = Viewer;
        var dbSkill = await _skillRepository.GetAsync(skill, currentMember.Organization);
        if (dbSkill is null)
        {
            return NotFound();
        }

        Skill = dbSkill;

        if (exemplarId is null)
        {
            return BadRequest();
        }

        var exemplar = Skill.Exemplars.SingleOrDefault(p => p.Id == exemplarId.Value);
        if (exemplar is null)
        {
            return NotFound();
        }

        await _skillRepository.RemoveExemplarAsync(exemplar, Viewer);

        return RedirectToPage(null,
            new {
                skill
            });
    }

    public async Task<IActionResult> OnPostCreateAsync(string skill)
    {
        var currentMember = Viewer;
        var dbSkill = await _skillRepository.GetAsync(skill, currentMember.Organization);
        if (dbSkill is null)
        {
            return NotFound();
        }

        // Compute embeddings for the prompt
        var exemplar = Exemplar.Require();
        var embeddings =
            await _openAIClient.CreateEmbeddingsAsync(exemplar, Model.AdaTextEmbedding, Organization);

        await _skillRepository.AddExemplarAsync(
            dbSkill,
            exemplar,
            new()
            {
                Arguments = ExpectedArguments.Require(),
                EmbeddingVector = embeddings.Data.Single().Embedding.Select(f => (double)f).ToArray(),
            },
            Viewer);

        return RedirectToPage(null,
            new {
                skill
            });
    }

    public async Task<IActionResult> OnPostTestExtractorAsync(string skill)
    {
        var currentMember = Viewer;
        var dbSkill = await _skillRepository.GetAsync(skill, currentMember.Organization);
        if (dbSkill is null)
        {
            return NotFound();
        }

        var result = await _recognizer.RecognizeArgumentsAsync(
            dbSkill,
            dbSkill.Exemplars,
            TestMessage ?? string.Empty,
            Viewer);
        var output = result.Arguments is { Length: > 0 }
            ? result.Arguments
            : "<No arguments found>";
        return TurboUpdate(TestExtractorResultId, output);
    }

    public async Task<IActionResult> OnPostUpdateSettingsAsync(string skill, bool argumentExtraction)
    {
        var currentMember = Viewer;
        var dbSkill = await _skillRepository.GetAsync(skill, currentMember.Organization);
        if (dbSkill is null)
        {
            return NotFound();
        }

        dbSkill.Properties = dbSkill.Properties with
        {
            ArgumentExtractionEnabled = argumentExtraction
        };

        await _skillRepository.UpdateAsync(dbSkill, Viewer.User);
        return Content("Ok");
    }
}
