[Setup]
AppName=ChattyBuddy
AppVersion=2.3
DefaultDirName={pf}\ChattyBuddy
DefaultGroupName=ChattyBuddy
Compression=lzma
SolidCompression=yes
SetupIconFile="C:\Users\fj\Documents\Developments\ChattyBuddy\ChattyBuddy.Wpf\Assets\AppIcon.ico"

[Files]
Source: "C:\Users\fj\Documents\Developments\ChattyBuddy\ChattyBuddy.Wpf\bin\Release\net10.0-windows\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\ChattyBuddy"; Filename: "{app}\ChattyBuddy.exe"

[Run]
Filename: "{app}\ChattyBuddy.exe"; Description: "Launch ChattyBuddy"; Flags: nowait postinstall skipifsilent

[Code]
var
  TailscalePage: TWizardPage;
  TailscaleInstallerPath: String;

function IsTailscaleInstalled(): Boolean;
var
  KeyNames: TArrayOfString;
  I: Integer;
  DisplayName: String;
begin
  Result := False;

  if RegGetSubkeyNames(HKLM, 'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall', KeyNames) then
  begin
    for I := 0 to GetArrayLength(KeyNames) - 1 do
    begin
      if RegQueryStringValue(HKLM,
        'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\' + KeyNames[I],
        'DisplayName', DisplayName) then
      begin
        if Pos('Tailscale', DisplayName) > 0 then
        begin
          Result := True;
          Exit;
        end;
      end;
    end;
  end;

  if not Result then
    if RegGetSubkeyNames(HKLM64, 'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall', KeyNames) then
    begin
      for I := 0 to GetArrayLength(KeyNames) - 1 do
      begin
        if RegQueryStringValue(HKLM64,
          'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\' + KeyNames[I],
          'DisplayName', DisplayName) then
        begin
          if Pos('Tailscale', DisplayName) > 0 then
          begin
            Result := True;
            Exit;
          end;
        end;
      end;
    end;
end;

procedure InitializeWizard();
begin
  TailscaleInstallerPath := ExpandConstant('{tmp}\tailscale-setup.exe');

  TailscalePage := CreateCustomPage(
    wpWelcome,
    'Tailscale Required',
    'ChattyBuddy needs Tailscale to connect'
  );

  with TNewStaticText.Create(TailscalePage) do
  begin
    Parent := TailscalePage.Surface;
    Caption := 'Tailscale is required for ChattyBuddy to function.'#13#10 +
               'If it is not installed, clicking Next will download the installer.';
    AutoSize := True;
    Top := ScaleY(10);
    Left := ScaleX(10);
    Width := TailscalePage.SurfaceWidth - ScaleX(20);
  end;
end;

function ShouldSkipPage(PageID: Integer): Boolean;
begin
  Result := False;
  if (PageID = TailscalePage.ID) and IsTailscaleInstalled() then
    Result := True;
end;

function DownloadWithBits(Url, Dest: string): Boolean;
var
  ResultCode: Integer;
begin
  Result := Exec(
    'powershell.exe',
    '-NoProfile -ExecutionPolicy Bypass -Command "Start-BitsTransfer -Source ''' + Url + ''' -Destination ''' + Dest + '''"',
    '',
    SW_SHOW,
    ewWaitUntilTerminated,
    ResultCode
  ) and (ResultCode = 0);
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var
  ResultCode: Integer;
begin
  Result := True;

  if CurPageID = TailscalePage.ID then
  begin
    if IsTailscaleInstalled() then
      exit;

    WizardForm.StatusLabel.Caption := 'Downloading Tailscale installer...';
    WizardForm.StatusLabel.Update;

    if not DownloadWithBits(
      'https://pkgs.tailscale.com/stable/tailscale-setup-latest.exe',
      TailscaleInstallerPath) then
    begin
      MsgBox('Download failed. Please check your internet connection and try again.', mbError, MB_OK);
      Result := False;
      exit;
    end;

    MsgBox('The Tailscale installer will now open. Install it, then return here and click Next again.', mbInformation, MB_OK);

    Exec(TailscaleInstallerPath, '', '', SW_SHOWNORMAL, ewNoWait, ResultCode);

    Result := False;
  end;
end;
