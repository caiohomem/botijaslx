using Botijas.Domain.Entities;
using Botijas.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Botijas.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SettingsController : ControllerBase
{
    private readonly BotijasDbContext _dbContext;

    public SettingsController(BotijasDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<AppSettingsResponse>> Get(CancellationToken cancellationToken)
    {
        var settings = await _dbContext.AppSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.AppSettingsId == AppSettings.SingletonId, cancellationToken)
            ?? AppSettings.CreateDefault();

        return Ok(MapResponse(settings));
    }

    [HttpPut]
    public async Task<ActionResult<AppSettingsResponse>> Update(
        [FromBody] UpdateAppSettingsRequest request,
        CancellationToken cancellationToken)
    {
        var settings = await _dbContext.AppSettings
            .FirstOrDefaultAsync(s => s.AppSettingsId == AppSettings.SingletonId, cancellationToken);

        if (settings is null)
        {
            settings = AppSettings.CreateDefault();
            await _dbContext.AppSettings.AddAsync(settings, cancellationToken);
        }

        settings.Update(
            storeName: request.StoreName.Trim(),
            storePhone: request.StorePhone.Trim(),
            storeLink: request.StoreLink.Trim(),
            appTitle: request.AppTitle.Trim(),
            whatsAppMessageTemplate: request.WhatsAppMessageTemplate.Trim(),
            welcomeMessageTemplate: request.WelcomeMessageTemplate.Trim(),
            thankYouMessageTemplate: request.ThankYouMessageTemplate.Trim(),
            labelTemplate: request.LabelTemplate.Trim(),
            printerType: NormalizePrinterType(request.PrinterType),
            labelWidthMm: Math.Clamp(request.LabelWidthMm, 10, 200),
            labelHeightMm: Math.Clamp(request.LabelHeightMm, 10, 200),
            maxPhoneDigits: Math.Clamp(request.MaxPhoneDigits, 1, 20),
            soundNotificationsDisabled: request.SoundNotificationsDisabled);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Ok(MapResponse(settings));
    }

    private static AppSettingsResponse MapResponse(AppSettings settings)
    {
        return new AppSettingsResponse(
            settings.StoreName,
            settings.StorePhone,
            settings.StoreLink,
            settings.AppTitle,
            settings.WhatsAppMessageTemplate,
            settings.WelcomeMessageTemplate,
            settings.ThankYouMessageTemplate,
            settings.LabelTemplate,
            settings.PrinterType,
            settings.LabelWidthMm,
            settings.LabelHeightMm,
            settings.MaxPhoneDigits,
            settings.SoundNotificationsDisabled,
            settings.UpdatedAt);
    }

    private static string NormalizePrinterType(string printerType)
    {
        return printerType.Equals("a4", StringComparison.OrdinalIgnoreCase) ? "a4" : "label";
    }
}

public record UpdateAppSettingsRequest(
    string StoreName,
    string StorePhone,
    string StoreLink,
    string AppTitle,
    string WhatsAppMessageTemplate,
    string WelcomeMessageTemplate,
    string ThankYouMessageTemplate,
    string LabelTemplate,
    string PrinterType,
    int LabelWidthMm,
    int LabelHeightMm,
    int MaxPhoneDigits,
    bool SoundNotificationsDisabled);

public record AppSettingsResponse(
    string StoreName,
    string StorePhone,
    string StoreLink,
    string AppTitle,
    string WhatsAppMessageTemplate,
    string WelcomeMessageTemplate,
    string ThankYouMessageTemplate,
    string LabelTemplate,
    string PrinterType,
    int LabelWidthMm,
    int LabelHeightMm,
    int MaxPhoneDigits,
    bool SoundNotificationsDisabled,
    DateTime UpdatedAt);
