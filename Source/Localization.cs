using System;
using System.Collections.Generic;
using System.IO;
using System.Security;

namespace JxrToSdr {
public static class UiText {
    public static bool Russian { get; set; }
    public static string SettingsPath { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"PariPari Anti-HDR","language.txt"); } }
    static readonly Dictionary<string,string> Translations=new Dictionary<string,string> {
        { "This file is not supported HDR scRGB. Format: ", "Этот файл не является поддерживаемым HDR scRGB. Формат: " },
        { "Image is too large (45 megapixel limit).", "Слишком большое изображение (лимит 45 мегапикселей)." },
        { "Could not find an available file name.", "Не удалось выбрать свободное имя файла." },
        { "Add files (Ctrl+O)", "Добавить файлы (Ctrl+O)" },
        { "Open folder", "Открыть папку" },
        { "Clear list", "Очистить список" },
        { "FILES · 0", "ФАЙЛЫ · 0" },
        { "Delete — remove the selected file from the list", "Delete — убрать выбранный файл из списка" },
        { "Actual size / fit to window", "Масштаб 1:1 / вписать в окно" },
        { "Brightness", "Яркость" },
        { "Color, %", "Цвет, %" },
        { "Reset adjustments", "Сбросить настройки" },
        { "Auto", "Авто" },
        { "Automatic adjustments", "Автоматическая настройка" },
        { "Adjust each image automatically. Drag the point to fine-tune. Click again for manual mode.", "Автоподстройка каждого кадра. Точку можно поправить вручную. Повторное нажатие — ручной режим." },
        { "Save selected", "Сохранить выбранный" },
        { "Save all", "Сохранить все" },
        { "Show PNG in folder", "Показать PNG в папке" },
        { "Stop", "Остановить" },
        { "Scroll to zoom · drag to pan · double-click to fit", "Колесо — масштаб · ЛКМ — перемещение · двойной щелчок — вписать в окно" },
        { "Finishing the current file. You can then close the window.", "Завершаем текущий файл. Затем можно закрыть окно." },
        { "Auto adjustment failed: ", "Ошибка автонастройки: " },
        { "JXR  ·  ready to convert", "JXR  ·  готов к обработке" },
        { "PNG saved", "PNG сохранён" },
        { "HDR screenshots (*.jxr;*.wdp;*.hdp)|*.jxr;*.wdp;*.hdp", "HDR-скриншоты (*.jxr;*.wdp;*.hdp)|*.jxr;*.wdp;*.hdp" },
        { "Select HDR screenshots", "Выберите HDR-скриншоты" },
        { "Could not open the folder picker", "Не удалось открыть выбор папки" },
        { "FILES · ", "ФАЙЛЫ · " },
        { "SDR preview", "SDR-превью" },
        { "The list is empty. Add files or drop them here", "Список пуст. Добавьте файлы или перетащите их в окно" },
        { "Could not open the folder", "Не удалось открыть папку" },
        { "Files in list: ", "Файлов в списке: " },
        { "No new JXR files found", "Новых JXR-файлов не найдено" },
        { "Loading HDR…", "Читаем HDR…" },
        { "Error: ", "Ошибка: " },
        { "Rendering SDR preview…", "Строим SDR-превью…" },
        { "SDR preview · saves at full resolution", "SDR-превью · при сохранении остаётся полное разрешение" },
        { "Select and load a JXR file first.", "Сначала выберите и загрузите JXR-файл." },
        { "Saving ", "Сохраняем " },
        { "Stopped. ", "Остановлено. " },
        { "Done. ", "Готово. " },
        { "Saved next to the originals: ", "Сохранено рядом с оригиналами: " },
        { " · Errors: ", " · Ошибок: " },
        { "Stopped · ", "Остановлено · " },
        { "Saved: ", "Сохранено: " },
        { "Some files could not be saved", "Некоторые файлы не сохранены" },
        { "Select a folder with JXR screenshots", "Выберите папку с JXR-скриншотами" },
        { "Brightness and saturation", "Яркость и насыщенность" },
        { "Brightness ↑", "Яркость ↑" },
        { "Saturation →", "Насыщенность →" },
        { "Image preview", "Предпросмотр изображения" },
        { "Drop an HDR screenshot / image here", "Перетащи HDR-скриншот / изображение" },
        { "Language", "Язык" },
        { "Language changed for this session. Could not save your preference.", "Язык изменён для этого сеанса. Не удалось сохранить выбор." }
    };
    public static string Get(string english) {
        string translated;return Russian&&Translations.TryGetValue(english,out translated)?translated:english;
    }
    public static void Load(string path) {
        Russian=false;
        try {Russian=File.ReadAllText(path).Trim()=="ru";}
        catch(IOException){}catch(UnauthorizedAccessException){}catch(SecurityException){}
    }
    public static bool Save(string path) {
        try {Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));File.WriteAllText(path,Russian?"ru":"en");return true;}
        catch(IOException){return false;}catch(UnauthorizedAccessException){return false;}catch(SecurityException){return false;}
    }
}
}
