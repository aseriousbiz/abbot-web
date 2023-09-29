using System.Globalization;
using System.Reflection;
using Xunit.Sdk;

namespace Serious.TestHelpers.CultureAware
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class UseCultureAttribute : BeforeAfterTestAttribute
    {
        readonly string _culture;
        readonly string _uiCulture;

        CultureInfo? _originalCulture;
        CultureInfo? _originalUiCulture;

        public UseCultureAttribute(string culture)
            : this(culture, culture) { }

        public UseCultureAttribute(string culture, string uiCulture)
        {
            _culture = culture;
            _uiCulture = uiCulture;
        }

        public override void Before(MethodInfo methodUnderTest)
        {
            _originalCulture = Thread.CurrentThread.CurrentCulture;
            _originalUiCulture = Thread.CurrentThread.CurrentUICulture;

            Thread.CurrentThread.CurrentCulture = new CultureInfo(_culture);
            Thread.CurrentThread.CurrentUICulture = new CultureInfo(_uiCulture);

            CultureInfo.CurrentCulture.ClearCachedData();
            CultureInfo.CurrentUICulture.ClearCachedData();
        }

        public override void After(MethodInfo methodUnderTest)
        {
            Thread.CurrentThread.CurrentCulture = _originalCulture ?? Thread.CurrentThread.CurrentCulture;
            Thread.CurrentThread.CurrentUICulture = _originalUiCulture ?? Thread.CurrentThread.CurrentUICulture;

            CultureInfo.CurrentCulture.ClearCachedData();
            CultureInfo.CurrentUICulture.ClearCachedData();
        }
    }
}
