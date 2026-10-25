using KaziWatts.Views;

namespace KaziWatts;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        // Écrans ouverts par-dessus les onglets (navigation par pile)
        Routing.RegisterRoute(nameof(SignUpPage), typeof(SignUpPage));
        Routing.RegisterRoute(nameof(ColocationPage), typeof(ColocationPage));
        Routing.RegisterRoute(nameof(MeterSetupPage), typeof(MeterSetupPage));
        Routing.RegisterRoute(nameof(MeterReadingPage), typeof(MeterReadingPage));
        Routing.RegisterRoute(nameof(SplitPage), typeof(SplitPage));
        Routing.RegisterRoute(nameof(RechargePage), typeof(RechargePage));
        Routing.RegisterRoute(nameof(BalancePage), typeof(BalancePage));
        Routing.RegisterRoute(nameof(RecordPaymentPage), typeof(RecordPaymentPage));
        Routing.RegisterRoute(nameof(ReceiptPage), typeof(ReceiptPage));
        Routing.RegisterRoute(nameof(RemindersPage), typeof(RemindersPage));
    }
}
