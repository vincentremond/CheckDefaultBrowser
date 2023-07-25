namespace CheckDefaultBrowser

open System
open System.Diagnostics
open System.Drawing
open System.Threading
open System.Windows.Forms
open Microsoft.FSharp.Core
open Microsoft.Toolkit.Uwp.Notifications
open System.IO
open Microsoft.Win32

[<RequireQualifiedAccess>]
module Log =
    let private openLog path =
        new StreamWriter(File.Open(path, FileMode.Append, FileAccess.Write, FileShare.Read))

    let private logFile =
        lazy
            ("D:\DAT\logs\CheckDefaultBrowser.txt"
             |> openLog)

    let write message =
        logFile.Value.WriteLine($"|{DateTimeOffset.UtcNow:O}| %s{message}")
        logFile.Value.Flush()

module Program =

    type MainForm() =
        inherit Form(Visible = false, ShowInTaskbar = false)

    type TrayIconContext(_cancellationTokenSource: CancellationTokenSource) =
        inherit ApplicationContext()

        let _notifyIcon =
            let icon = new Icon("icon.ico")

            let icon =
                new NotifyIcon(Icon = icon, Visible = true, Text = "Check Default Browser")

            icon.DoubleClick.Add(fun _ -> Application.Exit())

            ToastNotificationManagerCompat.add_OnActivated (fun args ->
                match args.Argument with
                | ""
                | "openSettings" ->
                    task {
                        let uri = Uri(@"ms-settings:defaultapps")

                        let! resultStatus =
                            Windows.System.Launcher
                                .LaunchUriAsync(uri)
                                .AsTask()

                        Log.write $"Launched settings: %b{resultStatus}"
                    }
                    |> Async.AwaitTask
                    |> Async.RunSynchronously
                | _ -> ()
            )

            icon

    let showPopup message =

        ToastContentBuilder()
            .AddText("Default Browser")
            .AddText(message)
            .SetToastDuration(ToastDuration.Long)
            .AddButton(ToastButton("Open Settings", "openSettings"))
            .Show()

    let check () =
        try
            let checkResult =
                [
                    "https", @"AppXx6s9gr5xzdkn58z6csv0r47y3ygytcx0"
                    "http", @"AppX5jyrnn5t4f5fc4fmtzbtfx9k7egk6j2k"
                ]
                |> List.tryPick (fun (protocol, expectedProgramId) ->

                    Log.write $"Checking {protocol}..."

                    let subKey =
                        Registry.CurrentUser.OpenSubKey(
                            $@"SOFTWARE\Microsoft\Windows\Shell\Associations\UrlAssociations\{protocol}\UserChoice"
                        )

                    let actualProgramId = subKey.GetValue("ProgId") :?> string

                    if
                        actualProgramId
                        <> expectedProgramId
                    then
                        Log.write $"Protocol {protocol} is not set to {expectedProgramId} but to {actualProgramId}"

                        Some {|
                            Protocol = protocol
                            ExpectedProgramId = expectedProgramId
                            ActualProgramId = actualProgramId
                        |}
                    else
                        Log.write $"OK, found '{actualProgramId}' as expected for protocol '{protocol}'"
                        None
                )

            match checkResult with
            | Some r ->
                showPopup $"Protocol {r.Protocol} is not set to {r.ExpectedProgramId} but to {r.ActualProgramId}"
            | None -> ()

        with ex ->
            Log.write $"Error: %s{string ex}"

    let startTimer (interval: TimeSpan) =
        let timer = new Timer()

        timer.Interval <-
            interval.TotalMilliseconds
            |> int

        timer.Tick.Add(fun _ -> check ())
        timer.Start()

    let stopOtherInstances () =
        let currentProcess = Process.GetCurrentProcess()
        let currentProcessId = currentProcess.Id
        let currentProcessName = currentProcess.ProcessName

        let concurrentProcesses =
            Process.GetProcessesByName(currentProcessName)
            |> Array.filter (fun p -> p.Id <> currentProcessId)

        for concurrentProcess in concurrentProcesses do
            concurrentProcess.Kill()

    [<STAThread>]
    [<EntryPoint>]
    let main _ =

        try
            stopOtherInstances ()

            Log.write "Starting app"

            check ()
            startTimer (TimeSpan.FromMinutes(1.0))

            let cancellationTokenSource = new CancellationTokenSource()
            Application.Run(new TrayIconContext(cancellationTokenSource))
        with ex ->
            Log.write $"Error: %s{string ex}"

        0
