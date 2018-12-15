﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.Video;
using Lanotalium.Project;
using Newtonsoft.Json;
using System.Text;
using System.Diagnostics;
using System.Security.Permissions;
using System.Security;
using UnityEngine.Events;
using NAudio.Wave;
using System.Threading.Tasks;
using UnityEngine.Networking;

public class LimProjectManager : MonoBehaviour
{
    public WindowsDialogUtility DialogUtils;
    public LimSystem SystemManager;
    public LimAutosaver Autosaver;
    public LimCloudManager CloudManager;
    public LimTunerManager TunerManager;

    public GameObject ProjectWizard;
    public RectTransform BGAScroll;
    public Image BGA0, BGA1, BGA2;
    public UnityEngine.UI.Button OpenChartPathDialogBtn;
    public InputField ProjectFolderPath, Name, Designer, MusicPath, ChartPath;
    public Text ProjectFolderLabel, NameLabel, DesignerLabel, MusicLabel, ChartLabel, BGALabel, WizardLabel, OpenLabel;
    private bool isCreateProject;

    public static LanotaliumProject CurrentProject = null;
    public static bool LapDirectOpened = false;
    public static string LapPath;
    public static string LapFolder
    {
        get
        {
            if (LapPath == null) return null;
            return Directory.GetParent(LapPath).FullName;
        }
    }

    private static bool HasNewDroppedLapFile = false;
    private static List<string> DroppedLapPaths;
    private static string ChartSaveLocation = string.Empty;
    private static UnityEvent CleanUpEvent = new UnityEvent();

    private void Start()
    {
        try
        {
            CleanUpEvent.Invoke();
            CleanUpEvent.RemoveAllListeners();
            if (Environment.GetCommandLineArgs().Length == 2 && !LapDirectOpened)
            {
                if (!File.Exists(Environment.GetCommandLineArgs()[1])) return;
                string ProjectFileString = File.ReadAllText(Environment.GetCommandLineArgs()[1]);
                CurrentProject = JsonConvert.DeserializeObject<LanotaliumProject>(ProjectFileString);
                if (CurrentProject == null) return;
                LapPath = Environment.GetCommandLineArgs()[1];
                StartCoroutine(LoadCurrentProject());
                LapDirectOpened = true;
                return;
            }
            if (LimChartZoneManager.OpenDownloadedChart)
            {
                LimChartZoneManager.OpenDownloadedChart = false;
                CurrentProject = JsonConvert.DeserializeObject<LanotaliumProject>(File.ReadAllText(LimChartZoneManager.DownloadedChartLapPath));
                if (CurrentProject == null) return;
                LapPath = LimChartZoneManager.DownloadedChartLapPath;
                StartCoroutine(LoadCurrentProject());
                return;
            }
            LimQuitBox.OnQuitBoxConfirmed.AddListener(SaveProject);
            if (CurrentProject != null) SaveProjectFile();
        }
        catch (Exception)
        {

        }
    }
    private void Update()
    {
        if (Input.GetKey(KeyCode.LeftControl))
        {
            if (Input.GetKeyDown(KeyCode.O))
            {
                LoadProject();
            }
            else if (Input.GetKeyDown(KeyCode.S))
            {
                SaveProject();
            }
        }
        if (HasNewDroppedLapFile)
        {
            HasNewDroppedLapFile = false;
            foreach (string P in DroppedLapPaths)
            {
                if (Path.GetExtension(P) == ".lap")
                {
                    LapPath = P;
                    InitializeProjectWizard(P);
                    return;
                }
                if (!File.Exists(P) && Directory.Exists(P))
                {
                    try
                    {
                        FileStream fs = new FileStream(P + "/info.bytes", FileMode.Open);
                        BinaryReader br = new BinaryReader(fs);
                        LanotaliumProject lp = new LanotaliumProject
                        {
                            Name = br.ReadString(),
                            Designer = br.ReadString()
                        };
                        br.Close();
                        fs.Close();
                        lp.ChartPath = Directory.GetFiles(P, "*.txt")[0];
                        lp.MusicPath = Directory.GetFiles(P, "*.ogg")[0];
                        if (File.Exists(P + "/background_linear.jpg"))
                        {
                            lp.BGA2Path = P + "/background.jpg";
                            lp.BGA1Path = P + "/background_gray.jpg";
                            lp.BGA0Path = P + "/background_linear.jpg";
                        }
                        else if (File.Exists(P + "/background_gray.jpg"))
                        {
                            lp.BGA1Path = P + "/background.jpg";
                            lp.BGA0Path = P + "/background_gray.jpg";
                        }
                        else
                        {
                            lp.BGA0Path = P + "/background.jpg";
                        }
                        File.WriteAllText(P + "/project.lap", JsonConvert.SerializeObject(lp));
                        LapPath = P + "/project.lap";
                        InitializeProjectWizard(LapPath);
                        return;
                    }
                    catch (Exception)
                    {

                    }
                }
            }
        }
    }
    public void SetTexts()
    {
        ProjectFolderLabel.text = LimLanguageManager.TextDict["Project_FolderLabel"];
        NameLabel.text = LimLanguageManager.TextDict["Project_Name"];
        DesignerLabel.text = LimLanguageManager.TextDict["Project_Designer"];
        MusicLabel.text = LimLanguageManager.TextDict["Project_Music"];
        ChartLabel.text = LimLanguageManager.TextDict["Project_Chart"];
        BGALabel.text = LimLanguageManager.TextDict["Project_BGA"];
        WizardLabel.text = LimLanguageManager.TextDict["Project_WizardLabel"];
        if (isCreateProject) OpenLabel.text = LimLanguageManager.TextDict["Project_Open_Create"];
        else OpenLabel.text = LimLanguageManager.TextDict["Project_Open_Open"];
    }

    public void CreateProject()
    {
        CurrentProject = new LanotaliumProject();
        ProjectWizard.SetActive(true);
        InitializeProjectWizard();
        LimTutorialManager.ShowTutorial("FirstProject3");
    }
    public void LoadProject()
    {
        string Path = WindowsDialogUtility.OpenFileDialog("", LimLanguageManager.TextDict["Project_LoadFilter"], LimSystem.Preferences.LastOpenedChartFolder);
        if (Path == null) return;
        LapPath = Path;
        InitializeProjectWizard(Path);
    }
    public void SaveProject()
    {
        if (LimSystem.ChartContainer == null) return;
        LimAutosaver.Autosave();
        string ChartPath = ChartSaveLocation;
        File.WriteAllText(ChartPath, LimSystem.ChartContainer.ChartData.ToString());
        CloudManager.UploadChart();
        LimNotifyIcon.ShowMessage(LimLanguageManager.NotificationDict["Project_Saved"]);
        SaveProjectFile();
    }
    public void SaveAsProject()
    {
        if (LimSystem.ChartContainer == null) return;
        string ChartPath = WindowsDialogUtility.SaveFileDialog("", "Chart (*.txt)|*.txt", "");
        if (ChartPath == null) return;
        if (!ChartPath.EndsWith(".txt")) ChartPath += ".txt";
        File.WriteAllText(ChartPath, LimSystem.ChartContainer.ChartData.ToString());
        LimNotifyIcon.ShowMessage(LimLanguageManager.NotificationDict["Project_Saved"]);
        if (LimSystem.Preferences.CloudAutosave) CloudManager.UploadChart();
        ChartSaveLocation = ChartPath;
        CurrentProject.ChartPath = ChartPath;
        SaveProjectFile();
    }

    //Create
    private void InitializeProjectWizard()
    {
        BGA0.sprite = null;
        BGA1.sprite = null;
        BGA2.sprite = null;
        BGAScroll.sizeDelta = new Vector2(0, 150);
        ProjectFolderPath.text = null;
        Name.text = null;
        Designer.text = null;
        MusicPath.text = null;
        ChartPath.text = LimLanguageManager.TextDict["Project_ChartWillGenerate"];
        isCreateProject = true;
    }

    //Load
    private void InitializeProjectWizard(string ProjectFilePath)
    {
        ChartPath.text = null;
        isCreateProject = false;
        string ProjectFileString = File.ReadAllText(ProjectFilePath);
        CurrentProject = JsonConvert.DeserializeObject<LanotaliumProject>(ProjectFileString);
        ProjectWizard.SetActive(true);
        if (CurrentProject == null)
        {
            DialogUtils.MessageBox.ShowMessage(LimLanguageManager.TextDict["Project_InvalidProjectFile"]);
            InitializeProjectWizard();
            return;
        }
        Name.text = CurrentProject.Name;
        Designer.text = CurrentProject.Designer;
        MusicPath.text = CurrentProject.MusicPath;
        ChartPath.text = CurrentProject.ChartPath;
        if (CurrentProject.BGACount() == 0)
        {
            DialogUtils.MessageBox.ShowMessage(LimLanguageManager.TextDict["Project_InvalidProjectFile"]);
            InitializeProjectWizard();
            return;
        }
        StartCoroutine(InitializeProjectWizardCoroutinePart());
    }
    IEnumerator InitializeProjectWizardCoroutinePart()
    {
        if (File.Exists(CurrentProject.BGA0Path))
        {
            WWW ImageRead = new WWW("file:///" + CurrentProject.BGA0Path);
            yield return ImageRead;
            if (ImageRead != null && string.IsNullOrEmpty(ImageRead.error))
            {
                Texture2D BackgroundImage = ImageRead.texture;
                BGA0.sprite = Sprite.Create(BackgroundImage, new Rect(0, 0, BackgroundImage.width, BackgroundImage.height), new Vector2(0.5f, 0.5f), 100);
            }
            BGAScroll.sizeDelta = new Vector2(266, 150);
        }
        if (File.Exists(CurrentProject.BGA1Path))
        {
            WWW ImageRead = new WWW("file:///" + CurrentProject.BGA1Path);
            yield return ImageRead;
            if (ImageRead != null && string.IsNullOrEmpty(ImageRead.error))
            {
                Texture2D BackgroundGray = ImageRead.texture;
                BGA1.sprite = Sprite.Create(BackgroundGray, new Rect(0, 0, BackgroundGray.width, BackgroundGray.height), new Vector2(0.5f, 0.5f), 100);
            }
            BGAScroll.sizeDelta = new Vector2(555, 150);
        }
        if (File.Exists(CurrentProject.BGA2Path))
        {
            WWW ImageRead = new WWW("file:///" + CurrentProject.BGA2Path);
            yield return ImageRead;
            if (ImageRead != null && string.IsNullOrEmpty(ImageRead.error))
            {
                Texture2D BackgroundLinear = ImageRead.texture;
                BGA2.sprite = Sprite.Create(BackgroundLinear, new Rect(0, 0, BackgroundLinear.width, BackgroundLinear.height), new Vector2(0.5f, 0.5f), 100);
            }
            BGAScroll.sizeDelta = new Vector2(845, 150);
        }
        ProjectFolderPath.text = CurrentProject.ProjectFolder;
    }

    //Wizard
    public void AddBGA()
    {
        if (CurrentProject.BGACount() >= 3) return;
        string Path = WindowsDialogUtility.OpenFileDialog("", "", CurrentProject.ProjectFolder);
        StartCoroutine(AddBGACoroutine(Path));
    }
    IEnumerator AddBGACoroutine(string Path)
    {
        Sprite Image;
        if (File.Exists(Path))
        {
            WWW ImageRead = new WWW("file:///" + Path);
            yield return ImageRead;
            if (ImageRead != null && string.IsNullOrEmpty(ImageRead.error))
            {
                Texture2D BackgroundImage = ImageRead.texture;
                Image = Sprite.Create(BackgroundImage, new Rect(0, 0, BackgroundImage.width, BackgroundImage.height), new Vector2(0.5f, 0.5f), 100);
            }
            else yield break;
        }
        else yield break;
        switch (CurrentProject.BGACount())
        {
            case 0:
                CurrentProject.BGA0Path = Path;
                BGA0.sprite = Image;
                BGAScroll.sizeDelta = new Vector2(266, 150);
                break;
            case 1:
                CurrentProject.BGA1Path = Path;
                BGA1.sprite = Image;
                SwapBGA01();
                BGAScroll.sizeDelta = new Vector2(555, 150);
                break;
            case 2:
                CurrentProject.BGA2Path = Path;
                BGA2.sprite = Image;
                SwapBGA12();
                SwapBGA01();
                BGAScroll.sizeDelta = new Vector2(845, 150);
                break;
        }
    }
    public void RemoveBGA()
    {
        if (CurrentProject.BGACount() <= 0) return;
        switch (CurrentProject.BGACount())
        {
            case 3:
                SwapBGA01();
                SwapBGA12();
                CurrentProject.BGA2Path = null;
                BGA2.sprite = null;
                BGAScroll.sizeDelta = new Vector2(555, 150);
                break;
            case 2:
                SwapBGA01();
                CurrentProject.BGA1Path = null;
                BGA1.sprite = null;
                BGAScroll.sizeDelta = new Vector2(266, 150);
                break;
            case 1:
                CurrentProject.BGA0Path = null;
                BGA0.sprite = null;
                BGAScroll.sizeDelta = new Vector2(0, 150);
                break;
        }
    }
    public void SwapBGA01()
    {
        string Tmp1;
        Sprite Tmp2;
        Tmp1 = CurrentProject.BGA0Path;
        CurrentProject.BGA0Path = CurrentProject.BGA1Path;
        CurrentProject.BGA1Path = Tmp1;
        Tmp2 = BGA0.sprite;
        BGA0.sprite = BGA1.sprite;
        BGA1.sprite = Tmp2;
    }
    public void SwapBGA12()
    {
        string Tmp1;
        Sprite Tmp2;
        Tmp1 = CurrentProject.BGA1Path;
        CurrentProject.BGA1Path = CurrentProject.BGA2Path;
        CurrentProject.BGA2Path = Tmp1;
        Tmp2 = BGA1.sprite;
        BGA1.sprite = BGA2.sprite;
        BGA2.sprite = Tmp2;
    }
    public void OnNameChanged()
    {
        CurrentProject.Name = Name.text;
    }
    public void OnDesignedChanged()
    {
        CurrentProject.Designer = Designer.text;
    }
    public void OpenProjectFolderDialog()
    {
        string Path = WindowsDialogUtility.OpenFolderDialog("");
        if (Path == null) return;
        ProjectFolderPath.text = Path;
        if (isCreateProject)
        {
            ChartPath.text = LimLanguageManager.TextDict["Project_ChartWillGenerate"];
            string EmptyChartPath = Path + "/EmptyChart.txt";
            if (File.Exists(EmptyChartPath)) EmptyChartPath = Path + "/EmptyChart_" + (new System.Random().Next(1000, 9999)).ToString() + ".txt";
            File.WriteAllText(Path + "/EmptyChart.txt", "{\"events\":null,\"eos\":0,\"bpm\":null,\"scroll\":null}");
            CurrentProject.ChartPath = Path + "/EmptyChart.txt";
            Name.text = new DirectoryInfo(Path).Name;
            CurrentProject.Name = Name.text;
            LapPath = Path + "/" + Name.text + ".lap";
        }
        LimTutorialManager.ShowTutorial("FirstProject4");
    }
    public void OpenChartDialog()
    {
        string Path = WindowsDialogUtility.OpenFileDialog("", "", CurrentProject.ProjectFolder);
        if (Path == null) return;
        ChartPath.text = Path;
        CurrentProject.ChartPath = Path;
    }
    public void OpenMusicDialog()
    {
        string Path = WindowsDialogUtility.OpenFileDialog("", "", CurrentProject.ProjectFolder);
        if (Path == null) return;
        MusicPath.text = Path;
        CurrentProject.MusicPath = Path;
        LimTutorialManager.ShowTutorial("FirstProject5");
    }
    public void WizardOpenProject()
    {
        if (CurrentProject.BGACount() == 0)
        {
            DialogUtils.MessageBox.ShowMessage(LimLanguageManager.TextDict["Project_NoBGA"]);
            return;
        }
        if (!CurrentProject.IsValid())
        {
            DialogUtils.MessageBox.ShowMessage(LimLanguageManager.TextDict["Project_InvalidProject"]);
            return;
        }
        StartCoroutine(LoadCurrentProject());
    }
    public void SaveProjectFile()
    {
        if (!TunerManager.isInitialized) return;
        if (!CurrentProject.IsValid()) return;
        try
        {
            string ProjectFile = JsonConvert.SerializeObject(CurrentProject);
            File.WriteAllText(isCreateProject ? (CurrentProject.ProjectFolder + "/" + CurrentProject.Name + ".lap") : LapPath, ProjectFile);
        }
        catch (Exception)
        {

        }
    }
    IEnumerator LoadCurrentProject()
    {
        bool isLoadFinished = false;
        DialogUtils.ProgressBar.ShowProgress(() => { return isLoadFinished; });
        #region Prepare Loading
        if (string.IsNullOrEmpty(CurrentProject.Name))
        {
            DialogUtils.MessageBox.ShowMessage(LimLanguageManager.TextDict["Project_NoName"]);
            isLoadFinished = true;
            yield break;
        }

        if (string.IsNullOrEmpty(CurrentProject.Designer))
        {
            DialogUtils.MessageBox.ShowMessage(LimLanguageManager.TextDict["Project_NoDesigner"]);
            isLoadFinished = true;
            yield break;
        }

        Lanotalium.ChartContainer LastChartContainer = LimSystem.ChartContainer;

        LimSystem.ChartContainer = new Lanotalium.ChartContainer
        {
            ChartProperty = new Lanotalium.Chart.ChartProperty(CurrentProject.ChartPath),
            ChartLoadResult = new Lanotalium.Chart.ChartLoadResult()
        };
        SystemManager.SavePreferences();
        DialogUtils.ProgressBar.Percent = 0.5f;
        #endregion
        #region Load Chart
        try
        {
            File.Copy(CurrentProject.ChartPath, CurrentProject.ChartPath.Replace(".txt", "_backup.txt"), true);
            string ChartJson = File.ReadAllText(CurrentProject.ChartPath);
            LimSystem.ChartContainer.ChartData = new Lanotalium.Chart.ChartData(ChartJson);
        }
        catch (Exception)
        {
            DialogUtils.MessageBox.ShowMessage(LimLanguageManager.TextDict["Project_ReadChartFailed"]);
            isLoadFinished = true;
            yield break;
        }
        LimSystem.ChartContainer.ChartLoadResult.isChartLoaded = true;
        DialogUtils.ProgressBar.Percent = 0.6f;
        #endregion
        #region Load Background Images
        string BGAColor = null, BGAGray = null, BGALinear = null;
        switch (CurrentProject.BGACount())
        {
            case 1:
                BGAColor = CurrentProject.BGA0Path;
                break;
            case 2:
                BGAGray = CurrentProject.BGA0Path;
                BGAColor = CurrentProject.BGA1Path;
                break;
            case 3:
                BGALinear = CurrentProject.BGA0Path;
                BGAGray = CurrentProject.BGA1Path;
                BGAColor = CurrentProject.BGA2Path;
                break;
        }
        DialogUtils.ProgressBar.Percent = 0.65f;

        if (CurrentProject.BGACount() >= 1)
        {
            WWW ImageRead = null;
            try
            {
                if (!File.Exists(BGAColor)) throw new FileNotFoundException();
                ImageRead = new WWW(Uri.EscapeUriString("file:///" + BGAColor));
            }
            catch (Exception)
            {
                DialogUtils.MessageBox.ShowMessage(LimLanguageManager.TextDict["Project_ReadImageFailed"]);
                isLoadFinished = true;
                yield break;
            }
            yield return ImageRead;
            try
            {
                if (ImageRead == null || !string.IsNullOrEmpty(ImageRead.error)) throw new Exception("Color Image Invalid");
                Texture2D BackgroundImage = ImageRead.texture;
                LimSystem.ChartContainer.ChartBackground.Color = Sprite.Create(BackgroundImage, new Rect(0, 0, BackgroundImage.width, BackgroundImage.height), new Vector2(0.5f, 0.5f), 100);
                LimSystem.ChartContainer.ChartLoadResult.isBackgroundLoaded = true;
            }
            catch (Exception)
            {
                DialogUtils.MessageBox.ShowMessage(LimLanguageManager.TextDict["Project_ReadImageFailed"]);
                isLoadFinished = true;
                yield break;
            }
        }
        DialogUtils.ProgressBar.Percent = 0.7f;
        if (CurrentProject.BGACount() >= 2)
        {
            WWW ImageRead = null;
            try
            {
                if (!File.Exists(BGAGray)) throw new FileNotFoundException();
                ImageRead = new WWW(Uri.EscapeUriString("file:///" + BGAGray));
            }
            catch (Exception)
            {
                DialogUtils.MessageBox.ShowMessage(LimLanguageManager.TextDict["Project_ReadImageFailed"]);
                isLoadFinished = true;
                yield break;
            }
            yield return ImageRead;
            try
            {
                if (ImageRead == null || !string.IsNullOrEmpty(ImageRead.error)) throw new Exception("Gray Image Invalid");
                Texture2D BackgroundGray = ImageRead.texture;
                LimSystem.ChartContainer.ChartBackground.Gray = Sprite.Create(BackgroundGray, new Rect(0, 0, BackgroundGray.width, BackgroundGray.height), new Vector2(0.5f, 0.5f), 100);
                LimSystem.ChartContainer.ChartLoadResult.isBackgroundGrayLoaded = true;
            }
            catch (Exception)
            {
                DialogUtils.MessageBox.ShowMessage(LimLanguageManager.TextDict["Project_ReadImageFailed"]);
                isLoadFinished = true;
                yield break;
            }
        }
        DialogUtils.ProgressBar.Percent = 0.8f;
        if (CurrentProject.BGACount() == 3)
        {
            WWW ImageRead = null;
            try
            {
                if (!File.Exists(BGALinear)) throw new FileNotFoundException();
                ImageRead = new WWW(Uri.EscapeUriString("file:///" + BGALinear));
            }
            catch (Exception)
            {
                DialogUtils.MessageBox.ShowMessage(LimLanguageManager.TextDict["Project_ReadImageFailed"]);
                isLoadFinished = true;
                yield break;
            }
            yield return ImageRead;
            try
            {
                if (ImageRead == null || !string.IsNullOrEmpty(ImageRead.error)) throw new Exception("Linear Image Invalid");
                Texture2D BackgroundLinear = ImageRead.texture;
                LimSystem.ChartContainer.ChartBackground.Linear = Sprite.Create(BackgroundLinear, new Rect(0, 0, BackgroundLinear.width, BackgroundLinear.height), new Vector2(0.5f, 0.5f), 100);
                LimSystem.ChartContainer.ChartLoadResult.isBackgroundLinearLoaded = true;
            }
            catch (Exception)
            {
                DialogUtils.MessageBox.ShowMessage(LimLanguageManager.TextDict["Project_ReadImageFailed"]);
                isLoadFinished = true;
                yield break;
            }
        }
        DialogUtils.ProgressBar.Percent = 0.9f;
        #endregion
        #region Load Music
        UnityWebRequest AudioRead = null;
        switch (Path.GetExtension(CurrentProject.MusicPath))
        {
            case ".wav":
                AudioRead = UnityWebRequestMultimedia.GetAudioClip(Uri.EscapeUriString("file:///" + CurrentProject.MusicPath), AudioType.WAV);
                break;
            case ".ogg":
                AudioRead = UnityWebRequestMultimedia.GetAudioClip(Uri.EscapeUriString("file:///" + CurrentProject.MusicPath), AudioType.OGGVORBIS);
                break;
            case ".mp3":
                AudioRead = UnityWebRequestMultimedia.GetAudioClip(Uri.EscapeUriString("file:///" + CurrentProject.MusicPath), AudioType.MPEG);
                break;
        }
        try
        {
            if (!File.Exists(CurrentProject.MusicPath)) throw new FileNotFoundException();
            if (AudioRead == null) throw new InvalidDataException();
        }
        catch (Exception)
        {
            DialogUtils.MessageBox.ShowMessage(LimLanguageManager.TextDict["Project_ReadMusicFailed"]);
            isLoadFinished = true;
            yield break;
        }
        yield return AudioRead.SendWebRequest();
        if (string.IsNullOrWhiteSpace(AudioRead.error))
        {
            try
            {
                LimSystem.ChartContainer.ChartMusic = new Lanotalium.Chart.ChartMusic(DownloadHandlerAudioClip.GetContent(AudioRead));
                LimSystem.ChartContainer.ChartLoadResult.isMusicLoaded = true;
            }
            catch (Exception)
            {
                DialogUtils.MessageBox.ShowMessage(LimLanguageManager.TextDict["Project_ReadMusicFailed"]);
                isLoadFinished = true;
                yield break;
            }
        }
        else
        {
            DialogUtils.MessageBox.ShowMessage(LimLanguageManager.TextDict["Project_ReadMusicFailed"]);
            isLoadFinished = true;
            yield break;
        }
        #endregion
        #region Load Video If Exist
        if (File.Exists(CurrentProject.ProjectFolder + "/background.mp4"))
        {
            LimSystem.ChartContainer.ChartBackground.VideoPath = CurrentProject.ProjectFolder + "/background.mp4";
            LimSystem.ChartContainer.ChartLoadResult.isBackgroundVideoDetected = true;
        }
        #endregion
        #region Handle Project File
        ChartSaveLocation = LimSystem.ChartContainer.ChartProperty.ChartPath;
        DialogUtils.ProgressBar.Percent = 0.95f;
        LimSystem.Preferences.LastOpenedChartFolder = LimSystem.ChartContainer.ChartProperty.ChartFolder;
        LimSystem.Preferences.Designer = CurrentProject.Designer;
        #endregion
        #region Clean Up
        CleanUpEvent.AddListener(() =>
        {
            if (LastChartContainer != null)
            {
                LastChartContainer.CleanUp();
                LastChartContainer = null;
                DestroyImmediate(BGA0.sprite);
                DestroyImmediate(BGA1.sprite);
                DestroyImmediate(BGA2.sprite);
                GC.Collect();
            }
        });
        #endregion
        AsyncOperation LoadAsync = SceneManager.LoadSceneAsync("LimTuner");
        while (!LoadAsync.isDone)
        {
            DialogUtils.ProgressBar.Percent = 0.95f + LoadAsync.progress * 0.05f;
            yield return null;
        }

        SaveProjectFile();
        DialogUtils.ProgressBar.Percent = 1f;
        isLoadFinished = true;
    }

    private void SerializeInt(int Number, FileStream ToStream)
    {
        byte[] Bytes = BitConverter.GetBytes(Number);
        ToStream.Write(Bytes, 0, Bytes.Length);
    }
    private void SerializeString(string Str, FileStream ToStream)
    {
        byte[] Bytes = Encoding.UTF8.GetBytes(Str);
        byte[] BytesCount = BitConverter.GetBytes(Bytes.Length);
        ToStream.Write(BytesCount, 0, BytesCount.Length);
        ToStream.Write(Bytes, 0, Bytes.Length);
    }
    private void SerializeFile(string Path, FileStream ToStream)
    {
        byte[] Bytes = File.ReadAllBytes(Path);
        byte[] BytesCount = BitConverter.GetBytes(Bytes.Length);
        ToStream.Write(BytesCount, 0, BytesCount.Length);
        ToStream.Write(Bytes, 0, Bytes.Length);
    }
    public void MakeRelease()
    {
        if (CurrentProject == null) return;
        if (!CurrentProject.IsValid()) return;

        string SavePath = Directory.GetParent(CurrentProject.ChartPath).FullName + "/" + CurrentProject.Name + ".larelease";
        FileStream Release = new FileStream(SavePath, FileMode.Create, FileAccess.Write);

        SerializeInt(CurrentProject.BGACount(), Release);
        SerializeString(CurrentProject.Name, Release);
        SerializeString(CurrentProject.Designer, Release);

        SerializeFile(CurrentProject.ChartPath, Release);
        SerializeString(Path.GetExtension(CurrentProject.MusicPath), Release);
        SerializeFile(CurrentProject.MusicPath, Release);
        if (CurrentProject.BGACount() >= 1) SerializeFile(CurrentProject.BGA0Path, Release);
        if (CurrentProject.BGACount() >= 2) SerializeFile(CurrentProject.BGA1Path, Release);
        if (CurrentProject.BGACount() == 3) SerializeFile(CurrentProject.BGA2Path, Release);

        Release.Close();

        Process.Start("explorer.exe", "/select," + SavePath.Replace("/", "\\"));
    }

    //DragAndDrop
    public void OnDragFile(List<string> Paths)
    {
        DroppedLapPaths = Paths;
        HasNewDroppedLapFile = true;
    }
}