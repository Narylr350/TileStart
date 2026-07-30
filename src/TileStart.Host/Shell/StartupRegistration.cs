using Microsoft.Win32;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Principal;
using TileStart.Host.Utilities;

namespace TileStart.Host.Shell;

public static class StartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "TileStart";
    private const int TaskCreateOrUpdate = 6;
    private const int TaskLogonInteractiveToken = 3;
    private const int TaskTriggerLogon = 9;
    private const int TaskActionExecute = 0;
    private const int TaskInstancesIgnoreNew = 2;
    private const int TaskRunLevelLeastPrivilege = 0;
    private const int TaskPriorityNormal = 4;
    private const int FileNotFoundHResult = unchecked((int)0x80070002);

    public static bool IsEnabled() => IsScheduledTaskEnabled() || IsLegacyRunEnabled();

    public static bool SetEnabled(bool enabled)
    {
        if (!enabled)
        {
            return DeleteScheduledTask() & SetLegacyRunEnabled(false);
        }

        var executablePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return false;
        }

        if (RegisterScheduledTask(executablePath))
        {
            SetLegacyRunEnabled(false);
            return true;
        }

        return SetLegacyRunEnabled(true);
    }

    public static void MigrateLegacyRegistration()
    {
        if (!IsLegacyRunEnabled())
        {
            return;
        }

        var executablePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executablePath) || !RegisterScheduledTask(executablePath))
        {
            DiagnosticLog.Write("Login startup migration kept the Run-key fallback because task registration failed.");
            return;
        }

        SetLegacyRunEnabled(false);
        DiagnosticLog.Write("Login startup migrated from the delayed Run key to an immediate logon task.");
    }

    private static bool IsScheduledTaskEnabled()
    {
        object? service = null;
        object? root = null;
        object? task = null;
        try
        {
            service = CreateTaskService();
            if (service is null)
            {
                return false;
            }

            dynamic scheduler = service;
            scheduler.Connect();
            root = scheduler.GetFolder("\\");
            task = ((dynamic)root).GetTask(GetTaskName());
            return (bool)((dynamic)task).Enabled;
        }
        catch (COMException exception) when (exception.HResult == FileNotFoundHResult)
        {
            return false;
        }
        catch (Exception exception) when (exception is COMException or UnauthorizedAccessException or SecurityException)
        {
            return false;
        }
        finally
        {
            ReleaseComObject(task);
            ReleaseComObject(root);
            ReleaseComObject(service);
        }
    }

    private static bool RegisterScheduledTask(string executablePath)
    {
        object? service = null;
        object? root = null;
        object? definition = null;
        object? registrationInfo = null;
        object? settings = null;
        object? principal = null;
        object? triggers = null;
        object? trigger = null;
        object? actions = null;
        object? action = null;
        object? registeredTask = null;
        try
        {
            var userSid = WindowsIdentity.GetCurrent().User?.Value;
            if (string.IsNullOrWhiteSpace(userSid))
            {
                return false;
            }

            service = CreateTaskService();
            if (service is null)
            {
                return false;
            }

            dynamic scheduler = service;
            scheduler.Connect();
            root = scheduler.GetFolder("\\");
            definition = scheduler.NewTask(0);

            dynamic taskDefinition = definition;
            registrationInfo = taskDefinition.RegistrationInfo;
            ((dynamic)registrationInfo).Description = "Launch TileStart immediately when the current user signs in.";

            settings = taskDefinition.Settings;
            dynamic taskSettings = settings;
            taskSettings.Enabled = true;
            taskSettings.StartWhenAvailable = true;
            taskSettings.DisallowStartIfOnBatteries = false;
            taskSettings.StopIfGoingOnBatteries = false;
            taskSettings.ExecutionTimeLimit = "PT0S";
            taskSettings.MultipleInstances = TaskInstancesIgnoreNew;
            taskSettings.Priority = TaskPriorityNormal;

            principal = taskDefinition.Principal;
            dynamic taskPrincipal = principal;
            taskPrincipal.UserId = userSid;
            taskPrincipal.LogonType = TaskLogonInteractiveToken;
            taskPrincipal.RunLevel = TaskRunLevelLeastPrivilege;

            triggers = taskDefinition.Triggers;
            trigger = ((dynamic)triggers).Create(TaskTriggerLogon);
            ((dynamic)trigger).UserId = userSid;

            actions = taskDefinition.Actions;
            action = ((dynamic)actions).Create(TaskActionExecute);
            dynamic execAction = action;
            execAction.Path = executablePath;
            execAction.WorkingDirectory = Path.GetDirectoryName(executablePath) ?? string.Empty;

            registeredTask = ((dynamic)root).RegisterTaskDefinition(
                GetTaskName(),
                taskDefinition,
                TaskCreateOrUpdate,
                null,
                null,
                TaskLogonInteractiveToken,
                null);
            return registeredTask is not null;
        }
        catch (Exception exception) when (exception is COMException or UnauthorizedAccessException or SecurityException)
        {
            DiagnosticLog.Write($"Unable to register immediate login task: {exception.Message}");
            return false;
        }
        finally
        {
            ReleaseComObject(registeredTask);
            ReleaseComObject(action);
            ReleaseComObject(actions);
            ReleaseComObject(trigger);
            ReleaseComObject(triggers);
            ReleaseComObject(principal);
            ReleaseComObject(settings);
            ReleaseComObject(registrationInfo);
            ReleaseComObject(definition);
            ReleaseComObject(root);
            ReleaseComObject(service);
        }
    }

    private static bool DeleteScheduledTask()
    {
        object? service = null;
        object? root = null;
        try
        {
            service = CreateTaskService();
            if (service is null)
            {
                return false;
            }

            dynamic scheduler = service;
            scheduler.Connect();
            root = scheduler.GetFolder("\\");
            ((dynamic)root).DeleteTask(GetTaskName(), 0);
            return true;
        }
        catch (COMException exception) when (exception.HResult == FileNotFoundHResult)
        {
            return true;
        }
        catch (Exception exception) when (exception is COMException or UnauthorizedAccessException or SecurityException)
        {
            return false;
        }
        finally
        {
            ReleaseComObject(root);
            ReleaseComObject(service);
        }
    }

    private static object? CreateTaskService()
    {
        var type = Type.GetTypeFromProgID("Schedule.Service", throwOnError: false);
        return type is null ? null : Activator.CreateInstance(type);
    }

    private static string GetTaskName()
    {
        var userSid = WindowsIdentity.GetCurrent().User?.Value ?? Environment.UserName;
        return $"TileStart-{userSid}";
    }

    private static bool IsLegacyRunEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            return key?.GetValue(ValueName) is string value && !string.IsNullOrWhiteSpace(value);
        }
        catch (SecurityException)
        {
            return false;
        }
    }

    private static bool SetLegacyRunEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
            if (enabled)
            {
                var executablePath = Environment.ProcessPath;
                if (string.IsNullOrWhiteSpace(executablePath))
                {
                    return false;
                }

                key.SetValue(ValueName, $"\"{executablePath}\"");
            }
            else
            {
                key.DeleteValue(ValueName, false);
            }

            return true;
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or SecurityException or IOException)
        {
            return false;
        }
    }

    private static void ReleaseComObject(object? value)
    {
        if (value is not null && Marshal.IsComObject(value))
        {
            Marshal.FinalReleaseComObject(value);
        }
    }
}
