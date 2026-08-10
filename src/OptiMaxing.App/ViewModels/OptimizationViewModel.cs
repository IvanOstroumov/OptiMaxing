using OptiMaxing.Core.Model;

namespace OptiMaxing.App.ViewModels;

public sealed class OptimizationViewModel(IOptimization model) : ObservableObject
{
    private bool _isSelected;
    private ApplyState _state = ApplyState.Unknown;
    private string _currentValueText = string.Empty;

    public IOptimization Model { get; } = model;

    /// <summary>Non-null for tweaks that pick a value rather than flip a switch; the card then shows
    /// a dropdown and the live system value instead of a plain checkbox.</summary>
    public IChoiceOptimization? Choice => Model as IChoiceOptimization;

    public bool HasChoices => Choice is not null;

    public IReadOnlyList<TweakChoice> Choices => Choice?.Choices ?? [];

    public TweakChoice? SelectedChoice
    {
        get => Choice?.Choices.FirstOrDefault(c => c.Id == Choice.SelectedChoiceId);
        set
        {
            if (Choice is null || value is null || value.Id == Choice.SelectedChoiceId)
            {
                return;
            }

            Choice.SelectedChoiceId = value.Id;
            OnPropertyChanged(nameof(SelectedChoice));

            // Picking a different value changes what "applied" means, so the state badge has to be
            // recomputed against the new selection rather than waiting for the next full scan.
            _ = RefreshCurrentValueAsync();
        }
    }

    /// <summary>"Сейчас в системе: 42" — the real value read from the machine, not what this app
    /// last wrote. Empty for tweaks that are a plain on/off switch.</summary>
    public string CurrentValueText
    {
        get => _currentValueText;
        private set => SetField(ref _currentValueText, value);
    }

    public bool HasCurrentValue => Choice is not null;

    public async Task RefreshCurrentValueAsync()
    {
        if (Choice is null)
        {
            return;
        }

        var raw = await Choice.DescribeCurrentAsync(CancellationToken.None);
        var known = await Choice.GetCurrentChoiceAsync(CancellationToken.None);

        CurrentValueText = known is null
            ? $"Сейчас в системе: {raw} — значение задано не нами"
            : $"Сейчас в системе: {raw} ({known.DisplayName})";

        State = await Choice.GetStateAsync(CancellationToken.None);
    }

    public string DisplayName => Model.DisplayName;
    public string Description => Model.Description;
    public string? TradeOff => Model.TradeOff;
    public string Category => Model.Category;
    public RiskLevel Risk => Model.Risk;
    public Reversibility Reversibility => Model.Reversibility;
    public bool RequiresRestart => Model.RequiresRestart;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetField(ref _isSelected, value);
    }

    public ApplyState State
    {
        get => _state;
        set
        {
            if (SetField(ref _state, value))
            {
                OnPropertyChanged(nameof(StateText));
                OnPropertyChanged(nameof(HasWarning));
            }
        }
    }

    public string StateText => State switch
    {
        ApplyState.Applied => "применено",
        ApplyState.NotApplied => "не применено",
        ApplyState.Modified => "изменено не нами",
        ApplyState.NotApplicable => "неприменимо",
        _ => "проверяется…",
    };

    /// <summary>Modified means some other tool owns this setting; reverting could surprise the user.</summary>
    public bool HasWarning => State == ApplyState.Modified;

    public string RiskText => Risk switch
    {
        RiskLevel.Safe => "Безопасно",
        RiskLevel.Caution => "Осторожно",
        RiskLevel.Advanced => "Продвинутое",
        _ => "Рекомендация",
    };

    public string ReversibilityText => Reversibility switch
    {
        Reversibility.Reversible => "Обратимо",
        Reversibility.ReversibleWithCaveat => "Обратимо частично",
        _ => "НЕОБРАТИМО",
    };

    public bool Matches(string query) =>
        string.IsNullOrWhiteSpace(query)
        || DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)
        || Description.Contains(query, StringComparison.OrdinalIgnoreCase)
        || Category.Contains(query, StringComparison.OrdinalIgnoreCase);
}
