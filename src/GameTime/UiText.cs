namespace GameTime;

internal enum AppLanguage { English, Russian }

internal static class UiText
{
    public static AppLanguage Language { get; set; } = AppLanguage.English;
    public static string SupportUrl => Language == AppLanguage.Russian
        ? "https://pay.cloudtips.ru/p/ae5b2218" : "https://buymeacoffee.com/alensp55";

    private static readonly Dictionary<string, string> English = new()
    {
        ["Приложения"] = "Apps",
        ["Правила"] = "Rules",
        ["Правила включены"] = "Enable rules",
        ["Учёт времени"] = "Time tracking",
        ["Не считать"] = "Don't track",
        ["Только активное окно"] = "Only the active window",
        ["Пока приложение запущено"] = "While the app is running",
        ["Дневной лимит"] = "Daily limit",
        ["Предупредить"] = "Notify me",
        ["Завершить приложение"] = "Terminate the app",
        ["Завершать по расписанию"] = "Scheduled closing",
        ["Выключено"] = "Off",
        ["Общее расписание"] = "Shared schedule",
        ["Своё расписание"] = "Custom schedule",
        ["Изменить расписание…"] = "Edit schedule…",
        ["Интервалы не заданы"] = "No intervals set",
        ["Выберите приложение слева"] = "Select an app on the left",
        ["Изменить ориентир…"] = "Edit daily target…",
        ["Мягкая цель для общего таймера. Приложения не завершаются."] =
            "A soft target for the overall timer. Apps will not be terminated.",
        ["Завершать в эти часы. Изменения затронут все приложения с общим расписанием."] =
            "Terminate during these hours. Changes affect all apps using the shared schedule.",
        ["Завершать это приложение в указанные часы."] = "Terminate this app during the specified hours.",
        ["После общего ориентира"] = "At the overall daily target",
        ["Приостановлено"] = "Paused",
        ["Пока запущено"] = "While running",
        ["Активное окно"] = "Active window",
        ["Завершить"] = "Terminate",
        ["Завершать: "] = "Close: ",
        ["Нет действий"] = "No actions",
        ["Некорректное правило приложения."] = "Invalid application rule.",
        ["Неизвестная версия настроек."] = "Unknown settings version.",
        ["Достигнут дневной лимит: "] = "Daily limit reached: ",
        ["OneMoreTimer — настройки"] = "OneMoreTimer — settings",
        ["Время использования"] = "Usage time",
        ["Окно таймера"] = "Timer window",
        ["Завершение по расписанию"] = "Scheduled closing",
        ["Общие настройки"] = "General",
        ["Язык / Language"] = "Language",
        ["Например, Darktide.exe"] = "For example, Darktide.exe",
        ["Приложение"] = "Application",
        ["Сегодня, ч:мин"] = "Today, h:mm",
        ["Учёт"] = "Track",
        ["Лимит, ч:мин"] = "Limit, h:mm",
        ["Завершать"] = "Terminate",
        ["Изменить время…"] = "Edit time…",
        ["Настроить приложение…"] = "Application settings…",
        ["Удалить"] = "Delete",
        ["Добавить"] = "Add",
        ["Выбрать .exe"] = "Choose .exe",
        ["Убрать"] = "Remove",
        ["Включить"] = "Enable",
        ["ОК"] = "OK",
        ["Применить"] = "Apply",
        ["Режим учёта"] = "Tracking mode",
        ["Интервал опроса, сек."] = "Polling interval, seconds",
        ["минут"] = "minutes",
        ["Ориентир на день"] = "Daily target",
        ["Медленно пульсировать после 100%"] = "Pulse slowly after 100%",
        ["Запускать вместе с Windows"] = "Start with Windows",
        ["Показывать окно таймера"] = "Show timer window",
        ["Только при запущенной игре"] = "Only while a tracked app is running",
        ["Отображение"] = "Display",
        ["Когда показывать"] = "When to show",
        ["Экран"] = "Monitor",
        ["Положение"] = "Position",
        ["Отступ по горизонтали"] = "Horizontal offset",
        ["Отступ по вертикали"] = "Vertical offset",
        ["Непрозрачность, %"] = "Opacity, %",
        ["Предпросмотр на 15 секунд"] = "Preview for 15 seconds",
        ["Проверить расположение"] = "Preview position",
        ["В borderless таймер виден поверх игры. Для настоящего exclusive fullscreen выберите другой " +
        "монитор. Окно пропускает клики; положение меняется отступами выше."] =
            "In borderless mode, the timer appears over the game. For true exclusive fullscreen, " +
            "select another monitor. The window lets clicks pass through; adjust its position using " +
            "the offsets above.",
        ["Пока игровой .exe запущен (включая фон)"] = "While the app is running (including background)",
        ["Только когда окно игры активно"] = "Only while the app window is active",
        ["Слева сверху"] = "Top left",
        ["Справа сверху"] = "Top right",
        ["Слева снизу"] = "Bottom left",
        ["Справа снизу"] = "Bottom right",
        ["Монитор игры (авто; иначе основной)"] = "App monitor (auto; otherwise primary)",
        ["основной"] = "primary",
        ["сейчас отключён"] = "currently disconnected",
        ["Поддержать разработчика"] = "Buy me a coffee",
        ["Автозапуск"] = "Startup",
        ["Интервал общий для учёта времени и завершения приложений. Язык меняется после «Применить»."] =
            "The polling interval is shared by tracking and application termination. Language changes " +
            "after Apply.",
        ["Учёт и завершение переключаются щелчком. Лимит — щелчком, время — двойным."] =
            "Click Track or Terminate to toggle. Click the limit to edit it; double-click today's time " +
            "to correct it.",
        ["Сегодня  "] = "Today  ",
        ["Сегодня: "] = "Today: ",
        ["Настройки"] = "Settings",
        ["Список игр"] = "App list",
        ["Приложения (*.exe)|*.exe"] = "Applications (*.exe)|*.exe",
        ["Выберите игру"] = "Choose an application",
        ["Удалить приложение"] = "Delete application",
        ["Удалить {0} из учёта и статистики за сегодня?\nОбщий итог будет пересчитан. Удаление " +
        "сохраняется сразу."] =
            "Remove {0} from tracking and today's statistics?\nThe daily total will be recalculated. " +
            "Deletion is saved immediately.",
        ["Учитывать время"] = "Track time",
        ["Завершать при достижении лимита"] = "Terminate when the daily limit is reached",
        ["Дневной лимит, мин"] = "Daily limit, minutes",
        ["Несохранённые данные приложения могут быть потеряны."] = "Unsaved application data may be lost.",
        ["Отмена"] = "Cancel",
        ["Правило приложения"] = "Application rule",
        ["Весь день"] = "All day",
        ["Интервал принудительного завершения"] = "Scheduled closing interval",
        ["С"] = "From",
        ["До"] = "To",
        ["до"] = "to",
        ["Если конец раньше начала, интервал заканчивается на следующий день."] =
            "If the end is earlier than the start, the interval ends the next day.",
        ["Выберите дни и разные время начала и конца либо «Весь день»."] =
            "Select weekdays and different start and end times, or choose All day.",
        ["Расписание"] = "Schedule",
        ["Завершать приложения по расписанию"] = "Terminate applications on schedule",
        ["Имя приложения.exe"] = "Application name.exe",
        ["Дни недели"] = "Weekdays",
        ["Приложения для завершения"] = "Applications to terminate",
        ["Общие запрещённые интервалы"] = "Shared blocked intervals",
        ["Добавить интервал"] = "Add interval",
        ["Изменить"] = "Edit",
        ["В указанные интервалы приложения завершаются также при повторном запуске.\nНесохранённые " +
        "данные могут быть потеряны."] =
            "Apps are also terminated when restarted during a blocked interval.\nUnsaved data may be " +
            "lost.",
        ["Проверка по общему интервалу опроса из «Общих настроек»."] =
            "Checked at the shared polling interval set in General.",
        ["Список приложений"] = "Application list",
        ["Выберите приложение"] = "Choose an application",
        ["Изменить время — "] = "Edit time — ",
        ["Время за сегодня"] = "Today's time",
        ["ч"] = "h",
        ["мин"] = "min",
        ["Сохранить"] = "Save",
        ["Пауза"] = "Pause",
        ["Выход"] = "Exit",
        ["Продолжить"] = "Resume",
        ["Пауза учёта и завершения"] = "Pause tracking and termination",
        [" · Пауза"] = " · Paused",
        ["OneMoreTimer — таймер"] = "OneMoreTimer — timer",
        ["Не удалось сохранить настройки"] = "Could not save settings",
        ["Настройки сохранены, но автозапуск изменить не удалось.\n"] =
            "Settings saved, but startup could not be changed.\n",
        ["Ошибка определения игры: "] = "Could not detect applications: ",
        ["Принудительно завершены: "] = "Terminated: ",
        ["Не удалось завершить: "] = "Could not terminate: ",
        ["Дата изменилась. Выберите приложение заново."] = "The date has changed. Select the application again.",
        ["Удаление не выполнено"] = "Deletion failed",
        ["Изменение времени не сохранено"] = "Time correction was not saved",
        ["Статистика не сохранена: "] = "Statistics not saved: ",
        ["Учёт и принудительное завершение на паузе"] = "Tracking and termination are paused",
        ["Учёт приостановлен: сессия недоступна"] = "Tracking paused: session unavailable",
        ["Расписание включено"] = "Scheduled closing enabled",
        ["Добавьте приложения в список ниже"] = "Add apps on the Usage time tab",
        ["Выбранный монитор отключён; таймер перенесён на основной"] =
            "Selected monitor disconnected; timer moved to primary",
        ["Ожидание приложения · опрос раз в {0} сек."] = "Waiting for apps · checking every {0} seconds",
        ["Учитываются: "] = "Tracking: ",
        ["\nВыйти с потерей несохранённого времени?"] = "\nExit and lose unsaved time?",
        ["Некорректный список игр или монитор."] = "Invalid application list or monitor.",
        ["Лимит приложения: от 1 до 1440 минут; без повторов имён."] =
            "App limits must be 1–1440 minutes, with no duplicate names.",
        ["Для завершения по лимиту включите учёт времени приложения."] =
            "Enable tracking to use daily limit termination.",
        ["Некорректное расписание."] = "Invalid schedule.",
        ["Некорректный интервал расписания."] = "Invalid schedule interval.",
        ["Неизвестный режим учёта или положение окна."] = "Unknown tracking mode, window position or language.",
        ["Интервал опроса: от 1 до 3600 секунд."] = "Polling interval must be 1–3600 seconds.",
        ["Ориентир: 0 (выключен) или 1–1440 минут. Непрозрачность: 30–100%."] =
            "Daily target: 0 (disabled) or 1–1440 minutes. Opacity: 30–100%.",
        ["Отступы должны быть от 0 до 10000."] = "Offsets must be 0–10000.",
        ["Укажите имя файла с расширением .exe, например Darktide.exe."] =
            "Enter a file name ending in .exe, such as Darktide.exe.",
        ["Нужно имя .exe без пути и специальных символов."] =
            "Enter an .exe name without a path or special characters.",
        ["Не удалось прочитать {0}. Данные не перезаписаны."] = "Could not read {0}. Data has not been overwritten.",
        ["Восстановлена резервная копия {0}. Последняя запись могла потеряться."] =
            "Recovered the backup of {0}. The most recent save may have been lost.",
        ["JSON не содержит данных."] = "JSON contains no data.",
        ["Укажите дни недели и время расписания."] = "Specify weekdays and schedule times.",
        ["Активный процесс недоступен для определения."] = "The active process cannot be identified.",
        ["Нельзя назначить принудительное завершение для {0}."] = "Forced termination cannot be enabled for {0}.",
        ["Некорректная дневная статистика."] = "Invalid daily statistics.",
        ["Некорректное время игры."] = "Invalid application time.",
        ["Повторяющаяся игра в статистике."] = "Duplicate application in statistics.",
        ["Строка уже отсутствует. Выберите игру заново."] = "The row no longer exists. Select the application again.",
        ["Дата изменилась. Откройте редактирование времени заново."] = "The date has changed. Reopen the time editor.",
        ["OneMoreTimer уже работает. Откройте настройки через значок в системном трее."] =
            "OneMoreTimer is already running. Open settings from its tray icon.",
        ["Не удалось определить путь приложения."] = "Could not determine the application path.",
        ["Для автозапуска запустите собранный OneMoreTimer.exe."] =
            "Run the built OneMoreTimer.exe to enable startup.",
        ["Не удалось запустить OneMoreTimer.\n"] = "Could not start OneMoreTimer.\n",
        ["\n\nДанные: "] = "\n\nData: ",
    };

    public static string Get(string text)
    {
        if (Language == AppLanguage.English)
            return English.GetValueOrDefault(text, text);
        foreach (var pair in English)
            if (pair.Value == text)
                return pair.Key;
        return text;
    }

    public static string Format(string text, params object[] args) => string.Format(Get(text), args);

    public static void Apply(Control control)
    {
        control.SuspendLayout();
        if (control is ComboBox combo && combo.Name != "Language")
        {
            int selected = combo.SelectedIndex;
            for (int index = 0; index < combo.Items.Count; index++)
                if (combo.Items[index] is string item)
                    combo.Items[index] = Get(item);
            combo.SelectedIndex = selected;
        }
        else if (control is not (TextBoxBase or UpDownBase or ComboBox or DateTimePicker))
            control.Text = Get(control.Text);
        if (control is TextBox textBox)
            textBox.PlaceholderText = Get(textBox.PlaceholderText);
        if (control is ListView list)
            foreach (ColumnHeader column in list.Columns)
                column.Text = Get(column.Text);
        foreach (Control child in control.Controls)
            Apply(child);
        control.ResumeLayout(true);
    }
}
