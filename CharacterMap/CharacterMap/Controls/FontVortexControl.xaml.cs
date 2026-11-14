using Windows.UI.Composition;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Markup;
using Windows.UI.Xaml.Media;

namespace CharacterMap.Controls;

[DependencyProperty<IEnumerable<CMFontFace>>("Faces", null, nameof(OnVortexParametersChanged))]
[DependencyProperty<string>("SampleText", null, nameof(OnVortexParametersChanged))]
[DependencyProperty<int>("MaxCards", 42, nameof(OnVortexParametersChanged))]
[DependencyProperty<int>("Arms", 6, nameof(OnVortexParametersChanged))]
[DependencyProperty<double>("Turns", 1.05, nameof(OnVortexParametersChanged))]
[DependencyProperty<double>("CycleDuration", 48.0, nameof(OnMotionParametersChanged))]
[DependencyProperty<double>("SpinDegreesPerMinute", -50.0, nameof(OnMotionParametersChanged))]
[DependencyProperty<double>("InnerScale", 0.45, nameof(OnVortexParametersChanged))]
[DependencyProperty<double>("WobbleAmplitude", 6.0, nameof(OnVortexParametersChanged))]
[DependencyProperty<double>("CardWidth", 148.0, nameof(OnVortexParametersChanged))]
[DependencyProperty<double>("CardHeight", 104.0, nameof(OnVortexParametersChanged))]
[DependencyProperty<bool>("RepeatFaces", true, nameof(OnVortexParametersChanged))]
[DependencyProperty<object>("CenterContent")]
[DependencyProperty<DataTemplate>("CenterContentTemplate")]
[DependencyProperty<DataTemplate>("CardTemplate", null, nameof(OnVortexParametersChanged))]
[ContentProperty(Name = nameof(CardTemplate))]
public sealed partial class FontVortexControl : UserControl
{
    private const float ArmJitter = 0.76f;
    private const float BirthFraction = 0.08f;
    private const float OuterRadiusFraction = 0.72f;
    private const float ExitMultiplier = 1.18f;

    private static readonly string[] DefaultSpecimens =
    [
        "Aa", "Gg", "Rr", "Qq", "&", "Ff", "Zz",
        "Bb", "Kk", "Ss", "Yy", "Mm", "Ee", "Ww"
    ];

    private readonly List<FrameworkElement> _activeCards = [];
    private CompositionPropertySet _vortexProperties;
    private bool _isLoaded;

    public event EventHandler<CMFontFace> FaceClicked;

    public FontVortexControl()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnVortexParametersChanged()
    {
        if (_isLoaded)
            RebuildVortex();
    }

    private void OnMotionParametersChanged()
    {
        if (_isLoaded)
            StartDriverAnimations();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = true;
        RebuildVortex();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = false;
        StopAndClearVortex();
    }

    public void RebuildVortex()
    {
        if (!_isLoaded)
            return;

        StopAndClearVortex();

        List<CMFontFace> sourceFaces = Faces?.Where(f => f is not null).ToList();
        if (sourceFaces is null || sourceFaces.Count == 0)
            return;

        int maxCards = Math.Max(1, MaxCards);
        int targetCount = RepeatFaces ? maxCards : Math.Min(sourceFaces.Count, maxCards);

        Visual hostVisual = RootGrid.GetElementVisual();
        EnsureVortexPropertySet(hostVisual.Compositor);

        for (int i = 0; i < targetCount; i++)
        {
            CMFontFace face = sourceFaces[i % sourceFaces.Count];
            FrameworkElement card = CreateSpecimenCard(face, i);

            card.PointerEntered += CardPointerEntered;
            card.PointerExited += CardPointerExited;


            _activeCards.Add(card);
            VortexCanvas.Children.Add(card);

            ConfigureCardComposition(card, hostVisual, i, targetCount);
        }

        StartDriverAnimations();
    }

    private Debouncer _pointerBouncer = new(32);

    private void CardPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        _pointerBouncer.Debounce(() =>
        {
            if (sender is FrameworkElement f)
                Canvas.SetZIndex(f, 100);
        });
    }

    private void CardPointerExited(object sender, PointerRoutedEventArgs e)
    {
        _pointerBouncer.Debounce(() =>
        {
            if (sender is FrameworkElement f)
                Canvas.SetZIndex(f, 0);
        });
    }

    private void EnsureVortexPropertySet(Compositor compositor)
    {
        if (_vortexProperties is null)
        {
            _vortexProperties = compositor.CreatePropertySet()
                .Insert("Phase", 0.0f)
                .Insert("Spin", 0.0f)
                .Insert("Wobble", 0.0f);
        }
        else
        {
            _vortexProperties
                .Insert("Phase", 0.0f)
                .Insert("Spin", 0.0f)
                .Insert("Wobble", 0.0f);
        }
    }

    private void ConfigureCardComposition(FrameworkElement card, Visual hostVisual, int index, int totalCards)
    {
        card.EnableCompositionTranslation();
        Visual cardVisual = card.GetElementVisual();
        CompositionFactory.StartCentering(cardVisual);

        int arms = Math.Max(1, Arms);
        int slotsPerArm = Math.Max(1, (int)MathF.Ceiling(totalCards / (float)arms));
        int arm = index % arms;
        int slot = index / arms;

        // Stagger initial progress along each spiral arm so arms do not pulse in lockstep rings
        float u0 = ((slot + 0.5f + (arm * ArmJitter)) / slotsPerArm) % 1.0f;

        float turns = Math.Max(0.25f, (float)Turns);
        float thetaMax = turns * MathF.PI * 2.0f;
        float thetaBirth = BirthFraction * thetaMax;
        float thetaExit = thetaMax * ExitMultiplier;
        float thetaSpan = thetaExit - thetaBirth;
        float armAngle = arm * (MathF.PI * 2.0f / arms);

        float theta0 = thetaBirth + (u0 * thetaSpan);
        float rNorm0 = (theta0 / thetaMax) * OuterRadiusFraction;
        float angle0 = theta0 + armAngle;
        Vector2 initialSpiralPos = new(rNorm0 * MathF.Cos(angle0), rNorm0 * MathF.Sin(angle0));

        // 1. Layout relatively using an Offset.XY ExpressionAnimation anchored to host.Size and card.Size
        ExpressionAnimation offsetAnimation = cardVisual
            .CreateExpressionAnimation("Offset.XY")
            .SetExpression("((host.Size - card.Size) * 0.5f) + (initialPos * Length(host.Size) * 0.5f)")
            .SetParameter("host", hostVisual)
            .SetParameter("card", cardVisual)
            .SetParameter("initialPos", initialSpiralPos);
        cardVisual.StartAnimation(offsetAnimation);

        // 2. Build the spiral conveyor expression state on the card visual's PropertySet
        cardVisual.Properties
            .Insert("u", u0)
            .Insert("Theta", theta0)
            .Insert("Angle", angle0)
            .Insert("RadiusNorm", rNorm0)
            .Insert("SpiralPos", initialSpiralPos)
            .Insert("Displacement", Vector2.Zero);

        cardVisual.Properties.StartAnimation(
            cardVisual.Properties.CreateExpressionAnimation("u")
                .SetExpression("Mod(u0 + vortex.Phase, 1.0f)")
                .SetParameter("u0", u0)
                .SetParameter("vortex", _vortexProperties));

        cardVisual.Properties.StartAnimation(
            cardVisual.Properties.CreateExpressionAnimation("Theta")
                .SetExpression("thetaBirth + (props.u * thetaSpan)")
                .SetParameter("thetaBirth", thetaBirth)
                .SetParameter("thetaSpan", thetaSpan)
                .SetParameter("props", cardVisual.Properties));

        cardVisual.Properties.StartAnimation(
            cardVisual.Properties.CreateExpressionAnimation("Angle")
                .SetExpression("props.Theta + armAngle + vortex.Spin")
                .SetParameter("armAngle", armAngle)
                .SetParameter("props", cardVisual.Properties)
                .SetParameter("vortex", _vortexProperties));

        cardVisual.Properties.StartAnimation(
            cardVisual.Properties.CreateExpressionAnimation("RadiusNorm")
                .SetExpression("(props.Theta / thetaMax) * rMaxFrac")
                .SetParameter("thetaMax", thetaMax)
                .SetParameter("rMaxFrac", OuterRadiusFraction)
                .SetParameter("props", cardVisual.Properties));

        cardVisual.Properties.StartAnimation(
            cardVisual.Properties.CreateExpressionAnimation("SpiralPos")
                .SetExpression("Vector2(props.RadiusNorm * Cos(props.Angle), props.RadiusNorm * Sin(props.Angle))")
                .SetParameter("props", cardVisual.Properties));

        // 3. Animate Translation building on the Displacement expression + harmonic floating wobble
        Vector2 wobblePhase = new(
            (index * 1.6180339f) % (MathF.PI * 2.0f),
            ((index * 2.6180339f) + 1.1f) % (MathF.PI * 2.0f));

        cardVisual.Properties.StartAnimation(
            cardVisual.Properties.CreateExpressionAnimation("Displacement")
                .SetExpression(
                    "((props.SpiralPos - initialPos) * Length(host.Size) * 0.5f) + " +
                    "(Vector2(Sin(vortex.Wobble + wobblePhase.X), Cos(vortex.Wobble + wobblePhase.Y)) * wobbleAmp)")
                .SetParameter("props", cardVisual.Properties)
                .SetParameter("vortex", _vortexProperties)
                .SetParameter("host", hostVisual)
                .SetParameter("initialPos", initialSpiralPos)
                .SetParameter("wobblePhase", wobblePhase)
                .SetParameter("wobbleAmp", (float)WobbleAmplitude));

        ExpressionAnimation translationAnimation = cardVisual
            .CreateExpressionAnimation(CompositionFactory.TRANSLATION)
            .SetExpression("Vector3(props.Displacement.X, props.Displacement.Y, 0.0f)")
            .SetParameter("props", cardVisual.Properties);
        cardVisual.StartAnimation(translationAnimation);

        // 4. Depth scale and inner/outer edge fade along the spiral trajectory
        float innerScale = Math.Clamp((float)InnerScale, 0.1f, 1.0f);
        ExpressionAnimation scaleAnimation = cardVisual
            .CreateExpressionAnimation(nameof(Visual.Scale))
            .SetExpression("Vector3(Lerp(innerScale, 1.0f, Clamp(props.u * 1.15f, 0.0f, 1.0f)), Lerp(innerScale, 1.0f, Clamp(props.u * 1.15f, 0.0f, 1.0f)), 1.0f)")
            .SetParameter("props", cardVisual.Properties)
            .SetParameter("innerScale", innerScale);
        cardVisual.StartAnimation(scaleAnimation);

        ExpressionAnimation opacityAnimation = cardVisual
            .CreateExpressionAnimation(nameof(Visual.Opacity))
            .SetExpression("Clamp(props.u / 0.16f, 0.0f, 1.0f) * Clamp((1.0f - props.u) / 0.18f, 0.0f, 1.0f)")
            .SetParameter("props", cardVisual.Properties);
        cardVisual.StartAnimation(opacityAnimation);
    }

    private void StartDriverAnimations()
    {
        if (_vortexProperties is null)
            return;

        if (!CompositionFactory.UISettings.AnimationsEnabled || !ResourceHelper.AllowAnimation)
        {
            _vortexProperties.StopAnimation("Phase");
            _vortexProperties.StopAnimation("Spin");
            _vortexProperties.StopAnimation("Wobble");
            return;
        }

        Compositor compositor = _vortexProperties.Compositor;
        CompositionEasingFunction linearEase = compositor.GetLinearEase();

        double cycleSeconds = Math.Max(4.0, CycleDuration);
        ScalarKeyFrameAnimation phaseAnimation = _vortexProperties
            .CreateScalarKeyFrameAnimation("Phase")
            .AddKeyFrame(0.0f, 0.0f)
            .AddKeyFrame(1.0f, 1.0f, linearEase)
            .SetDuration(cycleSeconds)
            .SetIterationBehavior(AnimationIterationBehavior.Forever);
        _vortexProperties.StartAnimation(phaseAnimation);

        double spinDegPerMin = SpinDegreesPerMinute;
        if (Math.Abs(spinDegPerMin) > 0.01)
        {
            float spinDirection = spinDegPerMin >= 0 ? 1.0f : -1.0f;
            double spinPeriodSeconds = 60.0 * (360.0 / Math.Abs(spinDegPerMin));

            ScalarKeyFrameAnimation spinAnimation = _vortexProperties
                .CreateScalarKeyFrameAnimation("Spin")
                .AddKeyFrame(0.0f, 0.0f)
                .AddKeyFrame(1.0f, spinDirection * MathF.PI * 2.0f, linearEase)
                .SetDuration(spinPeriodSeconds)
                .SetIterationBehavior(AnimationIterationBehavior.Forever);
            _vortexProperties.StartAnimation(spinAnimation);
        }
        else
            _vortexProperties.StopAnimation("Spin");

        ScalarKeyFrameAnimation wobbleAnimation = _vortexProperties
            .CreateScalarKeyFrameAnimation("Wobble")
            .AddKeyFrame(0.0f, 0.0f)
            .AddKeyFrame(1.0f, MathF.PI * 2.0f, linearEase)
            .SetDuration(4.0)
            .SetIterationBehavior(AnimationIterationBehavior.Forever);
        _vortexProperties.StartAnimation(wobbleAnimation);
    }

    private void StopAndClearVortex()
    {
        if (_vortexProperties is not null)
        {
            _vortexProperties.StopAnimation("Phase");
            _vortexProperties.StopAnimation("Spin");
            _vortexProperties.StopAnimation("Wobble");
        }

        foreach (FrameworkElement card in _activeCards)
        {
            card.Tapped -= OnCardTapped;
            Visual visual = card.GetElementVisual();
            visual.StopAnimation("Offset.XY");
            visual.StopAnimation(CompositionFactory.TRANSLATION);
            visual.StopAnimation(nameof(Visual.Scale));
            visual.StopAnimation(nameof(Visual.Opacity));
            visual.Properties.StopAnimation("u");
            visual.Properties.StopAnimation("Theta");
            visual.Properties.StopAnimation("Angle");
            visual.Properties.StopAnimation("RadiusNorm");
            visual.Properties.StopAnimation("SpiralPos");
            visual.Properties.StopAnimation("Displacement");
        }

        _activeCards.Clear();
        VortexCanvas.Children.Clear();
    }

    private FrameworkElement CreateSpecimenCard(CMFontFace face, int index)
    {
        // 1. Use defined Template
        if (CardTemplate is not null)
        {
            ContentPresenter item =  new ()
            {
                Content = face,
                ContentTemplate = CardTemplate,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Stretch,
            };

            return item;
        }

        // 2. Create fallback card
        Brush cardBackground = ResourceHelper.Get<Brush>("CardBackgroundFillColorDefaultBrush")
            ?? ResourceHelper.Get<Brush>("SystemControlBackgroundChromeMediumLowBrush");
        Brush cardBorder = ResourceHelper.Get<Brush>("CardStrokeColorDefaultBrush")
            ?? ResourceHelper.Get<Brush>("SystemControlForegroundBaseLowBrush");

        Border card = new()
        {
            Width = CardWidth,
            Height = CardHeight,
            CornerRadius = new(10),
            Padding = new(12, 8, 12, 8),
            Background = cardBackground,
            BorderBrush = cardBorder,
            BorderThickness = new(1),
            Tag = face
        };

        string tooltip = string.IsNullOrWhiteSpace(face.PreferredName) || face.PreferredName == face.FamilyName
            ? face.FamilyName
            : $"{face.FamilyName} — {face.PreferredName}";
        ToolTipService.SetToolTip(card, tooltip);
        card.Tapped += OnCardTapped;

        Grid layout = new();
        layout.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) });
        layout.RowDefinitions.Add(new() { Height = GridLength.Auto });

        TextBlock specimenText = new()
        {
            Text = GetSpecimenText(face, index),
            FontSize = 34,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            TextTrimming = TextTrimming.Clip,
            IsTextScaleFactorEnabled = false,
            FontStretch = face.DirectWriteProperties.Stretch,
            FontStyle = face.DirectWriteProperties.Style,
            FontWeight = face.DirectWriteProperties.Weight,
            IsColorFontEnabled = face.SupportsColourRendering
        };

        if (!string.IsNullOrEmpty(face.DisplaySource))
            specimenText.FontFamily = (FontFamily)XamlBindingHelper.ConvertValue(typeof(FontFamily), face.DisplaySource);

        string displayTitle = string.IsNullOrWhiteSpace(face.FamilyName)
            ? face.PreferredName
            : string.IsNullOrWhiteSpace(face.PreferredName) || string.Equals(face.PreferredName, "Regular", StringComparison.OrdinalIgnoreCase)
                ? face.FamilyName
                : $"{face.FamilyName} {face.PreferredName}";

        TextBlock titleText = new()
        {
            Text = displayTitle ?? string.Empty,
            FontSize = 10.5,
            Opacity = 0.65,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Bottom,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxLines = 1,
            IsTextScaleFactorEnabled = false
        };
        Grid.SetRow(titleText, 1);

        layout.Children.Add(specimenText);
        layout.Children.Add(titleText);
        card.Child = layout;

        return card;
    }

    private string GetSpecimenText(CMFontFace face, int index)
    {
        if (!string.IsNullOrWhiteSpace(SampleText))
            return SampleText;

        string candidate = DefaultSpecimens[index % DefaultSpecimens.Length];
        if (!face.DirectWriteProperties.IsSymbolFont && face.Face is not null && face.Face.GetGlyphIndice(candidate[0]) != 0)
            return candidate;

        IReadOnlyList<Character> chars = face.GetCharacters();
        if (chars is not null && chars.Count > 0)
        {
            int startIdx = Math.Min(chars.Count - 1, 3 + ((index * 5) % Math.Max(1, chars.Count - 3)));
            Character first = chars[startIdx];
            if (startIdx + 1 < chars.Count)
                return $"{first.Char}{chars[startIdx + 1].Char}";

            return first.Char;
        }

        return candidate;
    }

    private void OnCardTapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: CMFontFace face })
            FaceClicked?.Invoke(this, face);
    }
}

