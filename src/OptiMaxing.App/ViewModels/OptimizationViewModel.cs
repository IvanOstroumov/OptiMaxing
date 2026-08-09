using OptiMaxing.Core.Model;

namespace OptiMaxing.App.ViewModels;

public sealed class OptimizationViewModel(IOptimization model) : ObservableObject
{
    private bool _isSelected;
    private ApplyState _state = ApplyState.Unknown;

    public IOptimization Model { get; } = model;

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
