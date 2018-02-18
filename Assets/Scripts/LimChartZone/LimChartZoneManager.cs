﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Newtonsoft.Json;
using System.IO;
using Lanotalium.ChartZone.WebApi;

public class LimChartZoneManager : MonoBehaviour
{
    public static int FromScene = 0;
    public static bool OpenDownloadedChart = false;
    public static string DownloadedChartLapPath = string.Empty;
    public static string BilibiliVideoPrefix = "https://www.bilibili.com/video/";
    public static string QrCodeProvider = "http://qr.liantu.com/api.php?text=";
    public Text HeadText, ChartNameText, DesignerText, NotesText, DownloadText, VideoText, RatingText, CopyRightText;
    public RectTransform ChartListContent;
    public GameObject ChartBarPrefab;
    private List<LimChartBarManager> BarManagers = new List<LimChartBarManager>();
    private bool LoadChartListFinished = false;

    void Start()
    {
        BilibiliVideoPrefix = RemoteSettings.GetString("BilibiliVideoPrefix", "https://www.bilibili.com/video/");
        QrCodeProvider = RemoteSettings.GetString("QrCodeProvider", "http://qr.liantu.com/api.php?text=");
        StartCoroutine(GetChartList());
    }
    public void SetTexts()
    {
        HeadText.text = LimLanguageManager.TextDict["ChartZone_Head"];
        ChartNameText.text = LimLanguageManager.TextDict["ChartZone_ChartName"];
        DesignerText.text = LimLanguageManager.TextDict["ChartZone_Designer"];
        NotesText.text = LimLanguageManager.TextDict["ChartZone_Notes"];
        DownloadText.text = LimLanguageManager.TextDict["ChartZone_Download"];
        VideoText.text = LimLanguageManager.TextDict["ChartZone_Video"];
        RatingText.text = LimLanguageManager.TextDict["ChartZone_Rating"];
        CopyRightText.text = LimLanguageManager.TextDict["ChartZone_CopyRight"].Replace("<br>", "\n");
    }
    IEnumerator GetChartList()
    {
        #region Seed
        /*List<Lanotalium.ChartZone.ChartZoneChart> charts = JsonConvert.DeserializeObject<List<Lanotalium.ChartZone.ChartZoneChart>>(File.ReadAllText(@"H:\Server\html\lanotalium\chartzone\ChartList.json"));
        foreach (var chart in charts)
        {
            yield return LimChartZoneWebApi.AddChart(new LimChartZoneWebApi.ChartDto()
            {
                ChartName = chart.ChartName,
                Designer = chart.Designer,
                NoteCount = chart.NoteCount,
                Size = chart.Size,
                BilibiliAvIndex = chart.BilibiliAvIndex
            });
        }
        Dictionary<string, int> rat = JsonConvert.DeserializeObject<Dictionary<string, int>>(File.ReadAllText(@"H:\Server\html\lanotalium\chartzone\Modelista\Rating.json"));
        foreach (string key in rat.Keys)
        {
            LimChartZoneWebApi.Rating rating = new LimChartZoneWebApi.Rating
            {
                Rate = rat[key],
                UserId = key
            };
            yield return LimChartZoneWebApi.PostRating(14, rating);
        }*/
        #endregion
        ObjectWrap<List<ChartDto>> Charts = new ObjectWrap<List<ChartDto>>();
        yield return LimChartZoneWebApi.GetAllCharts(Charts);

        int HeightCount = 0;
        foreach (ChartDto Chart in Charts.Reference)
        {
            GameObject tGO = Instantiate(ChartBarPrefab, ChartListContent);
            tGO.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, HeightCount);
            tGO.GetComponent<LimChartBarManager>().Initialize(Chart);
            BarManagers.Add(tGO.GetComponent<LimChartBarManager>());
            HeightCount -= 50;
        }
        LoadChartListFinished = true;
    }
    public void SortByName()
    {
        if (!LoadChartListFinished) return;
        BarManagers.Sort((LimChartBarManager a, LimChartBarManager b) =>
        {
            return a.Name.CompareTo(b.Name);
        });
        int HeightCount = 0;
        foreach (LimChartBarManager chartBarManager in BarManagers)
        {
            chartBarManager.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, HeightCount);
            HeightCount -= 50;
        }
    }
    public void SortByDesigner()
    {
        if (!LoadChartListFinished) return;
        BarManagers.Sort((LimChartBarManager a, LimChartBarManager b) =>
        {
            return a.Designer.CompareTo(b.Designer);
        });
        int HeightCount = 0;
        foreach (LimChartBarManager chartBarManager in BarManagers)
        {
            chartBarManager.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, HeightCount);
            HeightCount -= 50;
        }
    }
    public void SortByRating()
    {
        if (!LoadChartListFinished) return;
        BarManagers.Sort((LimChartBarManager a, LimChartBarManager b) =>
        {
            return b.OnlineRating.CompareTo(a.OnlineRating);
        });
        int HeightCount = 0;
        foreach (LimChartBarManager chartBarManager in BarManagers)
        {
            chartBarManager.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, HeightCount);
            HeightCount -= 50;
        }
    }

    public void BackToLaunch()
    {
        SceneManager.LoadScene(FromScene);
    }
}
