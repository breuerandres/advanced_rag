namespace AdvancedRag.Api.Models.Users;

public sealed record SetAiBudgetRequest(decimal? MonthlyBudgetUsd, bool IsDisabled);
