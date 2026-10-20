using System;
using System.Collections.Generic;
using DragonBoyAccountManager.GUI.Common.Enums;
using DragonBoyAccountManager.GUI.Common.Models;
using DragonBoyAccountManager.GUI.ViewModels;

namespace DragonBoyAccountManager.GUI.Messaging;

public sealed record AccountSelectedMessage(AccountItemViewModel? Account);

public sealed record TargetAccountsChangedMessage(IReadOnlyList<Guid> TargetAccountIds);

public sealed record SpeedTargetAccountsChangedMessage(IReadOnlyList<Guid> TargetAccountIds, float Speed);

public sealed record ActiveTabChangedMessage(int OldIndex, int NewIndex);

public sealed record RequestNavigateToTabMessage(int TabIndex);

public sealed record SessionSnapshotUpdatedMessage(Guid AccountId, GameSessionStatus Status, GameSessionSnapshot? Snapshot);
