global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;

global using DragonBoyAccountManager.Core.Common;
global using DragonBoyAccountManager.Core.Common.Results;
global using DragonBoyAccountManager.Core.Engine;
global using DragonBoyAccountManager.Core.Engine.Abstractions;
global using DragonBoyAccountManager.Core.Engine.Events;
global using DragonBoyAccountManager.Core.Engine.Models;
global using DragonBoyAccountManager.Core.Sessions;
global using DragonBoyAccountManager.Core.Sessions.Validation;
global using DragonBoyAccountManager.Core.Telemetry.Snapshots;
global using DragonBoyAccountManager.Core.Telemetry.Scrapers;
global using DragonBoyAccountManager.Core.Execution;
global using DragonBoyAccountManager.Core.Execution.Commands;
global using DragonBoyAccountManager.Core.Automation.Behaviors;
global using DragonBoyAccountManager.Core.Automation.Behaviors.Combat;
global using DragonBoyAccountManager.Core.Automation.Behaviors.Lifecycle;
global using DragonBoyAccountManager.Core.Automation.Behaviors.Navigation;
global using DragonBoyAccountManager.Core.Accounts;
global using DragonBoyAccountManager.Core.Storage;

global using DragonBoyAccountManager.GUI.Domain.Settings;
global using DragonBoyAccountManager.GUI.Domain.Feedback;
global using DragonBoyAccountManager.GUI.Domain.Updates;

global using DragonBoyAccountManager.GUI.Application.Abstractions;
global using DragonBoyAccountManager.GUI.Application.Dispatching;
global using DragonBoyAccountManager.GUI.Application.Messaging;
global using DragonBoyAccountManager.GUI.Application.State;

global using DragonBoyAccountManager.GUI.Infrastructure.Engine;
global using DragonBoyAccountManager.GUI.Infrastructure.Feedback;
global using DragonBoyAccountManager.GUI.Infrastructure.Input;
global using DragonBoyAccountManager.GUI.Infrastructure.OS;
global using DragonBoyAccountManager.GUI.Infrastructure.Storage;
global using DragonBoyAccountManager.GUI.Infrastructure.Updater;

global using DragonBoyAccountManager.GUI.Presentation.DesignSystem.Assists;
global using DragonBoyAccountManager.GUI.Presentation.DesignSystem.Controls;
global using DragonBoyAccountManager.GUI.Presentation.DesignSystem.Converters;
global using DragonBoyAccountManager.GUI.Presentation.DesignSystem.Helpers;
global using DragonBoyAccountManager.GUI.Presentation.Shell.Dialogs;

global using DragonBoyAccountManager.GUI.ViewModels;
global using DragonBoyAccountManager.GUI.Views;
