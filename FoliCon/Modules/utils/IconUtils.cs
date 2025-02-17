﻿using FoliCon.Models.Constants;
using FoliCon.Models.Enums;
using FoliCon.Modules.Media;
using NLog;
using PosterIcon = FoliCon.Models.Data.PosterIcon;

namespace FoliCon.Modules.utils;

public static class IconUtils
{
    private static readonly NLog.Logger Logger = LogManager.GetCurrentClassLogger();
    
    /// <summary>
    /// Creates Icons from PNG
    /// </summary>
    public static int MakeIco(string iconMode, string selectedFolder, DataTable pickedListDataTable,
        bool isRatingVisible = false, bool isMockupVisible = true)
    {
        Logger.Debug(
            "Creating Icons from PNG, Icon Mode: {IconMode}, Selected Folder: {SelectedFolder}, isRatingVisible: {IsRatingVisible}, isMockupVisible: {IsMockupVisible}",
            iconMode, selectedFolder, isRatingVisible, isMockupVisible);
        
        var iconProcessedCount = 0;
        var ratingVisibility = isRatingVisible ? "visible" : "hidden";
        var mockupVisibility = isMockupVisible ? "visible" : "hidden";
        var fNames = new List<string>();
        fNames.AddRange(Directory.GetDirectories(selectedFolder).Select(Path.GetFileName));
        foreach (var i in fNames)
        {
            var tempI = i;
            var targetFile = $@"{selectedFolder}\{i}\{i}.ico";
            var pngFilePath = $@"{selectedFolder}\{i}\{i}.png";
            if (File.Exists(pngFilePath) && !File.Exists(targetFile))
            {
                var rating = pickedListDataTable.AsEnumerable()
                    .Where(p => p["FolderName"].Equals(tempI))
                    .Select(p => p["Rating"].ToString())
                    .FirstOrDefault();
                var mediaTitle = pickedListDataTable.AsEnumerable()
                    .Where(p => p["FolderName"].Equals(tempI))
                    .Select(p => p["Title"].ToString())
                    .FirstOrDefault();
                BuildFolderIco(iconMode, pngFilePath, rating, ratingVisibility,
                    mockupVisibility, mediaTitle);
                iconProcessedCount += 1;
                
                Logger.Info("Icon Created for Folder: {Folder}", i);
                Logger.Debug("Deleting PNG File: {PngFilePath}", pngFilePath);
                
                File.Delete(pngFilePath); //<--IO Exception here
            }

            if (!File.Exists(targetFile)) continue;
            FileUtils.HideFile(targetFile);
            FileUtils.SetFolderIcon($"{i}.ico", $@"{selectedFolder}\{i}");
        }

        FileUtils.ApplyChanges(selectedFolder);
        SHChangeNotify(SHCNE.SHCNE_UPDATEITEM, SHCNF.SHCNF_PATHW, selectedFolder);
        return iconProcessedCount;
    }

    /// <summary>
    /// Converts From PNG to ICO
    /// </summary>
    /// <param name="iconMode">Icon Mode to generate Icon.</param>
    /// <param name="filmFolderPath"> Path where to save and where PNG is Downloaded</param>
    /// <param name="rating"> if Wants to Include rating on Icon</param>
    /// <param name="ratingVisibility">Show rating or NOT</param>
    /// <param name="mockupVisibility">Is Cover Mockup visible. </param>
    /// <param name="mediaTitle">Title of the media.</param>
    private static void BuildFolderIco(string iconMode, string filmFolderPath, string rating,
        string ratingVisibility, string mockupVisibility, string mediaTitle)
    {
        Logger.Debug("Converting From PNG to ICO, Icon Mode: {IconMode}, Film Folder Path: {FilmFolderPath}," +
                          " Rating: {Rating}, Rating Visibility: {RatingVisibility}, Mockup Visibility: {MockupVisibility}," +
                          " Media Title: {MediaTitle}",
            iconMode, filmFolderPath, rating, ratingVisibility, mockupVisibility, mediaTitle);
        
        if (!File.Exists(filmFolderPath))
        {
            Logger.Warn("PNG File Not Found: {FilmFolderPath}", filmFolderPath);
            return;
        }

        ratingVisibility = string.IsNullOrEmpty(rating) ? "Hidden" : ratingVisibility;
        if (!string.IsNullOrEmpty(rating) && rating != "10")
        {
            rating = !rating.Contains('.') ? $"{rating}.0" : rating;
        }

        Bitmap icon;
        if (iconMode == "Professional")
        {
            icon = new ProIcon(filmFolderPath).RenderToBitmap();
        }
        else
        {
            using var task = GlobalVariables.IconOverlayType() switch
            {
                IconOverlay.Legacy => StaTask.Start(() =>
                    new Views.PosterIcon(new PosterIcon(filmFolderPath, rating, ratingVisibility, mockupVisibility))
                        .RenderToBitmap()),
                IconOverlay.Alternate => StaTask.Start(() =>
                    new PosterIconAlt(new PosterIcon(filmFolderPath, rating, ratingVisibility, mockupVisibility))
                        .RenderToBitmap()),
                IconOverlay.Liaher => StaTask.Start(() =>
                    new PosterIconLiaher(new PosterIcon(filmFolderPath, rating, ratingVisibility, mockupVisibility))
                        .RenderToBitmap()),
                IconOverlay.Faelpessoal => StaTask.Start(() => new PosterIconFaelpessoal(new PosterIcon(
                    filmFolderPath, rating,
                    ratingVisibility, mockupVisibility, mediaTitle)).RenderToBitmap()),
                IconOverlay.FaelpessoalHorizontal => StaTask.Start(() => new PosterIconFaelpessoalHorizontal(
                    new PosterIcon(
                        filmFolderPath, rating,
                        ratingVisibility, mockupVisibility, mediaTitle)).RenderToBitmap()),
                _ => StaTask.Start(() =>
                    new Views.PosterIcon(new PosterIcon(filmFolderPath, rating, ratingVisibility, mockupVisibility))
                        .RenderToBitmap())
            };
            task.Wait();
            icon = task.Result;
        }
        Logger.Info("Converting PNG to ICO for Folder: {FilmFolderPath}", filmFolderPath);
        PngToIcoService.Convert(icon, filmFolderPath.Replace("png", "ico"));
        icon.Dispose();
        Logger.Debug("Icon Created for Folder: {Folder}", filmFolderPath);
    }
}