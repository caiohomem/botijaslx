namespace Botijas.Domain.Entities;

public class AppSettings
{
    public static readonly Guid SingletonId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public Guid AppSettingsId { get; private set; }
    public string StoreName { get; private set; }
    public string StorePhone { get; private set; }
    public string StoreLink { get; private set; }
    public string AppTitle { get; private set; }
    public string WhatsAppMessageTemplate { get; private set; }
    public string WelcomeMessageTemplate { get; private set; }
    public string ThankYouMessageTemplate { get; private set; }
    public string PrinterType { get; private set; }
    public int LabelWidthMm { get; private set; }
    public int LabelHeightMm { get; private set; }
    public bool DebugEnabled { get; private set; }
    public bool SoundNotificationsDisabled { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private AppSettings()
    {
        StoreName = "Oficina da Cerveja";
        StorePhone = string.Empty;
        StoreLink = string.Empty;
        AppTitle = string.Empty;
        WhatsAppMessageTemplate = "Olá {name}! As suas {count} botija(s) de CO₂ estão prontas para recolha. Visite-nos quando puder!";
        WelcomeMessageTemplate = "Obrigado por confiar na Oficina da Cerveja! A sua botija está segura connosco. Visite a nossa loja: {link}";
        ThankYouMessageTemplate = "Obrigado por utilizar o nosso serviço de enchimento. Obrigado, equipa da Oficina da Cerveja!";
        PrinterType = "label";
        LabelWidthMm = 50;
        LabelHeightMm = 75;
        DebugEnabled = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public static AppSettings CreateDefault()
    {
        return new AppSettings
        {
            AppSettingsId = SingletonId
        };
    }

    public void Update(
        string storeName,
        string storePhone,
        string storeLink,
        string appTitle,
        string whatsAppMessageTemplate,
        string welcomeMessageTemplate,
        string thankYouMessageTemplate,
        string printerType,
        int labelWidthMm,
        int labelHeightMm,
        bool debugEnabled,
        bool soundNotificationsDisabled)
    {
        StoreName = storeName;
        StorePhone = storePhone;
        StoreLink = storeLink;
        AppTitle = appTitle;
        WhatsAppMessageTemplate = whatsAppMessageTemplate;
        WelcomeMessageTemplate = welcomeMessageTemplate;
        ThankYouMessageTemplate = thankYouMessageTemplate;
        PrinterType = printerType;
        LabelWidthMm = labelWidthMm;
        LabelHeightMm = labelHeightMm;
        DebugEnabled = debugEnabled;
        SoundNotificationsDisabled = soundNotificationsDisabled;
        UpdatedAt = DateTime.UtcNow;
    }
}
