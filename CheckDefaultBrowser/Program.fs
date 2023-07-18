namespace CheckDefaultBrowser

open System
open System.Diagnostics
open System.Drawing
open System.Threading
open System.Windows.Forms
open Microsoft.FSharp.Core
open Microsoft.Toolkit.Uwp.Notifications
open Microsoft.Win32

module Program =

    type TrayIconContext(_cancellationTokenSource: CancellationTokenSource) =
        inherit ApplicationContext()

        let _notifyIcon =
            let icon = new Icon("icon.ico")

            let icon =
                new NotifyIcon(Icon = icon, Visible = true, Text = "Check Default Browser")

            icon.DoubleClick.Add(fun _ -> Application.Exit())
            icon

    let showPopup message =

        ToastContentBuilder()
            .AddText("Default Browser")
            .AddText(message)
            .SetToastDuration(ToastDuration.Long)
            .AddButton(ToastButton("Open Settings", "openSettings").SetBackgroundActivation())
            .Show()

    let check () =
        let protocols = [
            // "http", "AppX5jyrnn5t4f5fc4fmtzbtfx9k7egk6j2k"
            "https", "AppXx6s9gr5xzdkn58z6csv0r47y3ygytcx0"
        ]

        for protocol, expectedProgId in protocols do
            let subKey =
                Registry.CurrentUser.OpenSubKey(
                    $@"SOFTWARE\Microsoft\Windows\Shell\Associations\UrlAssociations\{protocol}\UserChoice"
                )

            let progId = subKey.GetValue("ProgId") :?> string

            if progId <> expectedProgId then
                showPopup ($"{protocol} is not set to\n{expectedProgId}\n\nCurrent value is\n{progId}")

    let startTimer (interval: TimeSpan) =
        let timer = new Timer()
        timer.Interval <- interval.TotalMilliseconds |> int
        timer.Tick.Add(fun _ -> check ())
        timer.Start()
        
    let stopOtherInstances () =
        let currentProcess = Process.GetCurrentProcess()
        let currentProcessId = currentProcess.Id
        let currentProcessName = currentProcess.ProcessName

        let concurrentProcesses = Process.GetProcessesByName(currentProcessName) |> Array.filter (fun p -> p.Id <> currentProcessId)
        for concurrentProcess in concurrentProcesses do
            concurrentProcess.Kill()

    [<STAThread>]
    [<EntryPoint>]
    let main _ =
        
        stopOtherInstances ()

        check ()
        startTimer (TimeSpan.FromMinutes(1.0))

        let cancellationTokenSource = new CancellationTokenSource()
        Application.Run(new TrayIconContext(cancellationTokenSource))


        0
