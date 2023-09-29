using System;
using Serious.Abbot.Entities;

namespace Serious.Abbot.Models;

public class ApiKeyViewModel
{
    readonly ApiKey _apiKey;

    public ApiKeyViewModel(ApiKey apiKey, bool showCopyButton = false)
    {
        Id = apiKey.Id;
        ShowCopyMessage = showCopyButton;
        _apiKey = apiKey;
        Token = showCopyButton ? _apiKey.Token : null;
    }

    public int Id { get; }

    public string Name => _apiKey.Name;
    public string MaskedToken => _apiKey.GetMaskedToken();
    public bool IsExpired => _apiKey.IsExpired;
    public string? Token { get; }
    public DateTime ExpirationDate => _apiKey.ExpirationDate;
    public bool ShowCopyMessage { get; }

    public DomId GetDomId() => _apiKey.GetDomId();
}
