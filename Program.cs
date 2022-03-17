using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace SimpleFileManager
{
    class Program
    {
        /// <summary>
        /// Тип элемента файловой структуры (Диск, Папка или Файл)
        /// </summary>
        public enum DirItemType
        {
            Drive,
            Folder,
            File
        }

        /// <summary>
        /// Тип выравнивания (пол левому краю, по центру, по правому краю)
        /// </summary>
        public enum AlignType
        {
            Left,
            Center,
            Right
        }
        
        /// <summary>
        /// Начальные положения курсора для разных элементов UI
        /// Используются для перевода курсора в нужное положение перед заполнением информацией
        /// </summary>
        public struct PanelCursors
        {
            // Панель отображения пути текущей директории
            public int dirInfoCurX;
            public int dirInfoCurY;

            // Первый элемент в таблице
            public int panelFileNameCurX;
            public int panelFileNameCurY;

            public int panelFileSizeCurX;
            public int panelFileSizeCurY;

            public int panelFileDateCurX;
            public int panelFileDateCurY;

            public int panelFileAttrCurX;
            public int panelFileAttrCurY;

            // Панель отображения пути выбранного элемента
            public int panelFilePathCurX;
            public int panelFilePathCurY;
        }
       
        /// <summary>
        /// Размеры элементов UI
        /// </summary>
        public struct UIDimensions
        {
            // Размер окна
            public int windowWidth;
            public int windowHeight;

            // Ширина рамки
            public int borderSize;

            // Размеры панели вывода структуры каталога
            public int panelWidth;
            public int panelHeight;
            public int panelInnerWidth;

            // Ширина столбца отображающая название диска/папки/файла в дереве каталогов
            public int panelRowFileWidth;
            // Ширина столбцов отображающая дополнительную информацию о диске/папке/файле в дереве каталогов
            public int panelRowInfoWidth;
        }
        
        /// <summary>
        /// Хранит информацию о элементе файловой структуры (имя, путь, тип)
        /// </summary>
        public struct DirectoryItem
        {
            public string Name;
            public string Path;
            public DirItemType Type;
        }
        
        /// <summary>
        /// Хранит информацию по навигации (первый элемент и последний элемент для отображения в панели, текущий выбранный элемент)
        /// Т.к. элементов в папке может быть больше, чем то количество элементов, которое можно вывести на экран,
        /// то эта структура используется для запоминание индексов элементов, которые выводим на экран.
        /// </summary>
        public struct NavigationInfo
        {
            public int FirstItem; // Первый отображаемый элемент в панели дерева каталогов
            public int LastItem;  // Последний отображаемый элемент в панели дерева каталогов
            public int SelectedItem; // Выбранный элемент в панели дерева каталогов
        }

        /// <summary>
        /// Содержит свойства кнопок всплывающих окон
        /// </summary>
        public struct PopUpMessageBtn
        {
            public bool isVisible;  // Видна ли кнопка (показывать или нет)
            public bool isActive;   // Активна ли кнопка (выбрана)
            public string Name;     // Название кнопки
        }

        // Содержит вычисленные размеры UI интерфейса
        static public UIDimensions uiDimensions = new UIDimensions();

        // Содержит начальные положения курсоров для левой и правой панелей
        static public PanelCursors cursorsPanelLeft = new PanelCursors();
        static public PanelCursors cursorsPanelRight = new PanelCursors();

        // Массив элементов каталога
        static public DirectoryItem[] panelLeftContent;
        static public DirectoryItem[] panelRightContent;

        // Навигационна информация для левого и правого каталогов
        static public NavigationInfo panelLeftNavigationInfo = new NavigationInfo();
        static public NavigationInfo panelRightNavigationInfo = new NavigationInfo();

        // Кнопки диалоговых окон
        static public PopUpMessageBtn btnCancel = new PopUpMessageBtn();
        static public PopUpMessageBtn btnConfirm = new PopUpMessageBtn();
        static public PopUpMessageBtn btnConfirmCopy = new PopUpMessageBtn();
        static public PopUpMessageBtn btnConfirmDelete = new PopUpMessageBtn();
        static public PopUpMessageBtn btnReplace = new PopUpMessageBtn();
        static public PopUpMessageBtn btnSkip = new PopUpMessageBtn();

        // Хранит текущую активную панель. Левая или правая.
        static bool isLeftActive = true;

        // Файл для сохранения ошибок
        const string errorLogFilename = "errorlog.txt";

        // Хранит путь корневой папки для текущего каталога
        static string rootPathLeft = "";
        static string rootPathRight = "";

        // Для сохранения/получения выбранного индекса в дереве каталога
        // при переходе в другую папку
        static Stack prevItemIndexLeft = new Stack();
        static Stack prevItemIndexRight = new Stack();

        // Используются для запоминания и отображения количества
        // каталогов, фаилов и занимаемого места при 
        // отображинии информации о кталоге
        static long totalDirs;
        static long totalFiles;
        static long totalSpace;

        // Символы для рисования рамок
        static char dlTopLeftCorner = '\u2554';
        static char dlHorizontalLine = '\u2550';
        static char dlTopRightCorner = '\u2557';
        static char dlTopMiddleCross = '\u2564';
        static char dlVerticalLine = '\u2551';
        static char dlVerticalRightCross = '\u2563';
        static char dlVerticalLeftCross = '\u2560';
        static char dlBottomLeftCorner = '\u255a';
        static char dlBottomRightCorner = '\u255d';
        static char dlBottomMiddleCross = '\u2567';

        static void Main(string[] args)
        {
            // Кнопка "Отмена" диалогового окна
            btnCancel.isActive = false;
            btnCancel.isVisible = false;
            btnCancel.Name = "Отмена";

            // Кнопка "Закрыть" диалогового окна
            btnConfirm.isActive = false;
            btnConfirm.isVisible = false;
            btnConfirm.Name = "Закрыть";

            // Кнопка "Скопировать" диалогового окна
            btnConfirmCopy.isActive = false;
            btnConfirmCopy.isVisible = false;
            btnConfirmCopy.Name = "Скопировать";

            // Кнопка "Удалить" диалогового окна
            btnConfirmDelete.isActive = false;
            btnConfirmDelete.isVisible = false;
            btnConfirmDelete.Name = "Удалить";

            // Кнопка "Заменить" диалогового окна
            btnReplace.isActive = false;
            btnReplace.isVisible = false;
            btnReplace.Name = "Заменить";

            // Кнопка "Пропустить" диалогового окна
            btnSkip.isActive = false;
            btnSkip.isVisible = false;
            btnSkip.Name = "Пропустить";

            // Устанавливаем размер окна приложения при старте
            InitConsoleWindowSizeAtStartup();

            // Рассчитываем размеры UI в зависимости от размера окна
            CalculateDimensions();

            // Вызываем функцию отрисовки UI по расчитанным позициям
            DrawUI();

            // Сбрасываем индексы навигации для левой и правой панелей
            panelLeftNavigationInfo.FirstItem = 0;
            panelLeftNavigationInfo.LastItem = 0;
            panelLeftNavigationInfo.SelectedItem = 0;

            panelRightNavigationInfo.FirstItem = 0;
            panelRightNavigationInfo.LastItem = 0;
            panelRightNavigationInfo.SelectedItem = 0;

            // Считываем сохраненные пути директорий, которые были открыты в панелях 
            ReadSavedRootPath();

            // Получаем структуру папки для левой панели
            DirectoryItem[] directoryItems = GetFolderData(rootPathLeft, cursorsPanelLeft, ref rootPathLeft);

            // Если не удалось получить структуру папок, то пробуем получить данные для каталога, из которого было запущено приложение
            if (directoryItems == null)
            {
                panelLeftContent = GetFolderData(AppContext.BaseDirectory, cursorsPanelLeft, ref rootPathLeft);
            }
            else
            {
                panelLeftContent = directoryItems;
            }

            // Получаем структуру папки для правой панели
            directoryItems = GetFolderData(rootPathRight, cursorsPanelRight, ref rootPathRight);

            // Если не удалось получить структуру папок, то пробуем получить данные для каталога, из которого было запущено приложение
            if (directoryItems == null)
            {
                panelRightContent = GetFolderData(AppContext.BaseDirectory, cursorsPanelRight, ref rootPathRight);
            }
            else
            {
                panelRightContent = directoryItems;
            }
            
            // Заполняем панели полученными данными
            FillUpPanel(panelLeftContent, cursorsPanelLeft, isLeftActive, ref panelLeftNavigationInfo);
            FillUpPanel(panelRightContent, cursorsPanelRight, !isLeftActive, ref panelRightNavigationInfo);

            ConsoleKeyInfo key = Console.ReadKey();


            while(key.Key != ConsoleKey.F10)
            {
                switch (key.Key)
                {
                    // Переход по дереву каталогов (файлов) вверх 
                    case (ConsoleKey.UpArrow):
                        {
                            if (isLeftActive)
                            {
                                // Если активна левая панель, то проверяем, что индекс выбранного элемента > 0
                                if (panelLeftNavigationInfo.SelectedItem > 0)
                                {
                                    // Если индекс выбранного элемента > 0, то уменьшаем его и перерисовываем левую панель
                                    --panelLeftNavigationInfo.SelectedItem;
                                    FillUpPanel(panelLeftContent, cursorsPanelLeft, isLeftActive, ref panelLeftNavigationInfo);
                                }
                                else
                                {
                                    panelLeftNavigationInfo.SelectedItem = 0;
                                }
                            }
                            else
                            {
                                // Если активна правая панель, то проверяем, что индекс выбранного элемента > 0
                                if (panelRightNavigationInfo.SelectedItem > 0)
                                {
                                    // Если индекс выбранного элемента > 0, то уменьшаем его и перерисовываем правую панель
                                    --panelRightNavigationInfo.SelectedItem;
                                    FillUpPanel(panelRightContent, cursorsPanelRight, !isLeftActive, ref panelRightNavigationInfo);
                                }
                                else
                                {
                                    panelRightNavigationInfo.SelectedItem = 0;
                                }
                            }
                        }
                        break;

                    // Переход по дереву каталогов (файлов) вниз
                    case (ConsoleKey.DownArrow):
                        {
                            
                            if (isLeftActive)
                            {
                                // Если активна левая панель, то проверяем, что индекс выбранного элемента > индекса последнего элемента
                                if (panelLeftNavigationInfo.SelectedItem < panelLeftContent.Length - 1)
                                {
                                    // Если индекс меньше, то увеличиваем его и перерисовываем левую панель
                                    ++panelLeftNavigationInfo.SelectedItem;
                                    FillUpPanel(panelLeftContent, cursorsPanelLeft, isLeftActive, ref panelLeftNavigationInfo);
                                }
                            }
                            else
                            {
                                // Если активна правая панель, то проверяем, что индекс выбранного элемента > индекса последнего элемента
                                if (panelRightNavigationInfo.SelectedItem < panelRightContent.Length - 1)
                                {
                                    // Если индекс меньше, то увеличиваем его и перерисовываем правую панель
                                    ++panelRightNavigationInfo.SelectedItem;
                                    FillUpPanel(panelRightContent, cursorsPanelRight, !isLeftActive, ref panelRightNavigationInfo);
                                }
                            }
                        }
                        break;

                    // Переход вначало дерева каталогов
                    case (ConsoleKey.LeftArrow):
                        {
                            if (isLeftActive)
                            {
                                // Сбрасываем индексы и перерисовываем паель
                                panelLeftNavigationInfo.SelectedItem = 0;
                                panelLeftNavigationInfo.FirstItem = 0;
                                panelLeftNavigationInfo.LastItem = 0;
                                FillUpPanel(panelLeftContent, cursorsPanelLeft, isLeftActive, ref panelLeftNavigationInfo);
                            }
                            else
                            {
                                // Сбрасываем индексы и перерисовываем паель
                                panelRightNavigationInfo.SelectedItem = 0;
                                panelRightNavigationInfo.FirstItem = 0;
                                panelRightNavigationInfo.LastItem = 0;
                                FillUpPanel(panelRightContent, cursorsPanelRight, !isLeftActive, ref panelRightNavigationInfo);
                            }
                        }
                        break;

                    // Переход в конец дерева каталогов
                    case (ConsoleKey.RightArrow):
                        {
                            if (isLeftActive)
                            {
                                // Сбрасываем индексы первого и последнего элементы для вывода
                                // Индеску выбранного элемента присваиваем индекс последнего элемента и перерисоваваем панель
                                panelLeftNavigationInfo.SelectedItem = panelLeftContent.Length-1;
                                panelLeftNavigationInfo.FirstItem = 0;
                                panelLeftNavigationInfo.LastItem = 0;
                                FillUpPanel(panelLeftContent, cursorsPanelLeft, isLeftActive, ref panelLeftNavigationInfo);
                            }
                            else
                            {
                                // Сбрасываем индексы первого и последнего элементы для вывода
                                // Индеску выбранного элемента присваиваем индекс последнего элемента и перерисоваваем панель
                                panelRightNavigationInfo.SelectedItem = panelRightContent.Length-1;
                                panelRightNavigationInfo.FirstItem = 0;
                                panelRightNavigationInfo.LastItem = 0;
                                FillUpPanel(panelRightContent, cursorsPanelRight, !isLeftActive, ref panelRightNavigationInfo);
                            }
                        }
                        break;
                    
                    // Переход между панелями
                    case (ConsoleKey.Tab):
                        {
                            // Меняем флаг активной панели на противоположный и перерисоваваем их
                            isLeftActive = !isLeftActive;
                            FillUpPanel(panelLeftContent, cursorsPanelLeft, isLeftActive, ref panelLeftNavigationInfo);
                            FillUpPanel(panelRightContent, cursorsPanelRight, !isLeftActive, ref panelRightNavigationInfo);
                        }
                        break;

                    // Вывод справки
                    case (ConsoleKey.F1):
                        {
                            // Выводим окно с название "Спарвка"
                            DrawPopUpMessageBorder("Справка");

                            // Печатаем в окне справочную информацию
                            ShowPopUpMessage(new string[] {
                                                " ",
                                                "Навигация по интерфейсу:",
                                                @"Стрелки - перемещение по каталогу внутри панели.",
                                                @"Enter   - перейти в выбранный каталог или запустить файл.",
                                                @"Tab     - для перехода между панелями.",
                                                @"Пробел  - вывод информации со свойствами каталога.",
                                                @" ",
                                                @"Атрибуты папок/файлов:",
                                                @"A - Archive",
                                                @"S - System",
                                                @"R - Read only",
                                                @"H - Hiden",
                                                @"E - Encrypted",
                            });

                            // Устанавливаем флаги для кнопки
                            btnConfirm.isVisible = true;
                            btnConfirm.isActive = true;
                            
                            // Выводим кнопку
                            DrawPopUpMessageBtns(new PopUpMessageBtn[] { btnConfirm });
                            
                            // Сбрасываем флаги для кнопки
                            btnConfirm.isVisible = false;
                            btnConfirm.isActive = false;
                            
                            // Ждем подтверждения от пользователя
                            Console.ReadKey();

                            // Перерисовываем UI
                            DrawUI();
                            FillUpPanel(panelLeftContent, cursorsPanelLeft, isLeftActive, ref panelLeftNavigationInfo);
                            FillUpPanel(panelRightContent, cursorsPanelRight, !isLeftActive, ref panelRightNavigationInfo);
                        }
                        break;

                    // Выбор диска
                    case ConsoleKey.F2:
                        {
                            if (isLeftActive)
                            {
                                // Получаем список доступных дисков. Если в запросе будет пустая строка вместо пути, то метод возвращает список дисков
                                directoryItems = GetFolderData("", cursorsPanelLeft, ref rootPathLeft);
                                
                                if (directoryItems != null)
                                {
                                    // Если удалось получить список дисков, то выводим информацию на экран
                                    // Запоминаем полученные данные для левой панели
                                    panelLeftContent = directoryItems;

                                    // Получаем выбранный индекс
                                    // Если был возврат из папки, то восстанавливаем сохраненный индекс
                                    // Если перешли в более глубокую папку, то сохраняем индекс
                                    //panelLeftNavigationInfo.SelectedItem = UpdateSelectedIndex(panelLeftNavigationInfo.SelectedItem, prevItemIndexLeft);
                                    panelLeftNavigationInfo.SelectedItem = 0;
                                    prevItemIndexLeft.Clear();

                                    // Сбрасываем индексы диапазона отображения
                                    panelLeftNavigationInfo.FirstItem = 0;
                                    panelLeftNavigationInfo.LastItem = 0;

                                    // Очищаем данные на экране
                                    ClearPanelData(cursorsPanelLeft);

                                    // Заполняем экран панели на экране новыми данными
                                    FillUpPanel(panelLeftContent, cursorsPanelLeft, isLeftActive, ref panelLeftNavigationInfo);
                                }
                            }
                            else
                            {
                                // Получаем список доступных дисков. Если в запросе будет пустая строка вместо пути, то метод возвращает список дисков
                                directoryItems = GetFolderData("", cursorsPanelRight, ref rootPathRight);

                                if (directoryItems != null)
                                {
                                    // Если удалось получить список дисков, то выводим информацию на экран
                                    // Запоминаем полученные данные для правой панели
                                    panelRightContent = directoryItems;

                                    // Получаем выбранный индекс
                                    // Если был возврат из папки, то восстанавливаем сохраненный индекс
                                    // Если перешли в более глубокую папку, то сохраняем индекс
                                    //panelRightNavigationInfo.SelectedItem = UpdateSelectedIndex(panelRightNavigationInfo.SelectedItem, prevItemIndexRight);
                                    panelRightNavigationInfo.SelectedItem = 0;
                                    prevItemIndexRight.Clear();

                                    // Сбрасываем индексы диапазона отображения
                                    panelRightNavigationInfo.FirstItem = 0;
                                    panelRightNavigationInfo.LastItem = 0;

                                    // Очищаем данные на экране
                                    ClearPanelData(cursorsPanelRight);

                                    // Заполняем экран панели на экране новыми данными
                                    FillUpPanel(panelRightContent, cursorsPanelRight, !isLeftActive, ref panelRightNavigationInfo);
                                }
                            }
                        }
                        break;

                    // Копирование папки или файла
                    case ConsoleKey.F5:
                        {
                            if (isLeftActive && (panelLeftNavigationInfo.SelectedItem > 0))
                            {
                                UICopyProcess(ref panelLeftContent, ref panelLeftNavigationInfo, ref panelRightContent, ref panelRightNavigationInfo, ref cursorsPanelRight, ref rootPathRight);
                                ClearPanelData(cursorsPanelRight);
                                DrawUI();
                                FillUpDirPanel(rootPathLeft, ref cursorsPanelLeft);
                                FillUpDirPanel(rootPathRight, ref cursorsPanelRight);
                                FillUpPanel(panelLeftContent, cursorsPanelLeft, isLeftActive, ref panelLeftNavigationInfo);
                                FillUpPanel(panelRightContent, cursorsPanelRight, !isLeftActive, ref panelRightNavigationInfo);
                            }
                            else if (panelRightNavigationInfo.SelectedItem > 0)
                            {
                                UICopyProcess(ref panelRightContent, ref panelRightNavigationInfo, ref panelLeftContent, ref panelLeftNavigationInfo, ref cursorsPanelLeft, ref rootPathLeft);
                                ClearPanelData(cursorsPanelLeft);
                                DrawUI();
                                FillUpDirPanel(rootPathLeft, ref cursorsPanelLeft);
                                FillUpDirPanel(rootPathRight, ref cursorsPanelRight);
                                FillUpPanel(panelLeftContent, cursorsPanelLeft, isLeftActive, ref panelLeftNavigationInfo);
                                FillUpPanel(panelRightContent, cursorsPanelRight, !isLeftActive, ref panelRightNavigationInfo);
                            }
                        }
                        break;

                    // Зарезервировано на будущее
                    case ConsoleKey.F6:
                        {
                            DrawUI();
                            FillUpDirPanel(rootPathLeft, ref cursorsPanelLeft);
                            FillUpDirPanel(rootPathRight, ref cursorsPanelRight);
                            FillUpPanel(panelLeftContent, cursorsPanelLeft, isLeftActive, ref panelLeftNavigationInfo);
                            FillUpPanel(panelRightContent, cursorsPanelRight, !isLeftActive, ref panelRightNavigationInfo);
                        }
                        break;

                    // Удаление выбранной папки или выбранного файла
                    case ConsoleKey.F8:
                        {
                            if (isLeftActive && (panelLeftNavigationInfo.SelectedItem > 0))
                            {
                                UIDeleteProcess(ref panelLeftContent, ref panelLeftNavigationInfo, ref cursorsPanelLeft, ref rootPathLeft);
                                ClearPanelData(cursorsPanelLeft);
                                DrawUI();
                                FillUpDirPanel(rootPathLeft, ref cursorsPanelLeft);
                                FillUpDirPanel(rootPathRight, ref cursorsPanelRight);
                                FillUpPanel(panelLeftContent, cursorsPanelLeft, isLeftActive, ref panelLeftNavigationInfo);
                                FillUpPanel(panelRightContent, cursorsPanelRight, !isLeftActive, ref panelRightNavigationInfo);
                            }
                            else if (panelRightNavigationInfo.SelectedItem > 0)
                            {

                                UIDeleteProcess(ref panelRightContent, ref panelRightNavigationInfo, ref cursorsPanelRight, ref rootPathRight);
                                ClearPanelData(cursorsPanelRight);
                                DrawUI();
                                FillUpDirPanel(rootPathLeft, ref cursorsPanelLeft);
                                FillUpDirPanel(rootPathRight, ref cursorsPanelRight);
                                FillUpPanel(panelLeftContent, cursorsPanelLeft, isLeftActive, ref panelLeftNavigationInfo);
                                FillUpPanel(panelRightContent, cursorsPanelRight, !isLeftActive, ref panelRightNavigationInfo);

                            }
                        }
                        break;

                    // Открытие папки или файла
                    case (ConsoleKey.Enter):
                        {
                            if (isLeftActive)
                            {
                                // получаем элементы выбранной папки, если файл, то выполняем его
                                directoryItems = GetFolderData(panelLeftContent[panelLeftNavigationInfo.SelectedItem].Path, cursorsPanelLeft, ref rootPathLeft);

                                // Если получили обновленные данные, сохраняем их для данной панели и перерисовываем.
                                if (directoryItems != null)
                                {
                                    panelLeftContent = directoryItems;

                                    panelLeftNavigationInfo.FirstItem = 0;
                                    panelLeftNavigationInfo.LastItem = 0;

                                    panelLeftNavigationInfo.SelectedItem = UpdateSelectedIndex(panelLeftNavigationInfo.SelectedItem, prevItemIndexLeft);

                                    ClearPanelData(cursorsPanelLeft);

                                    FillUpPanel(panelLeftContent, cursorsPanelLeft, isLeftActive, ref panelLeftNavigationInfo);
                                }
                            }
                            else
                            {
                                // получаем элементы выбранной папки, если файл, то выполняем его
                                directoryItems = GetFolderData(panelRightContent[panelRightNavigationInfo.SelectedItem].Path, cursorsPanelRight, ref rootPathRight);

                                // Если получили обновленные данные, сохраняем их для данной панели и перерисовываем.
                                if (directoryItems != null)
                                {
                                    panelRightContent = directoryItems;

                                    panelRightNavigationInfo.SelectedItem = UpdateSelectedIndex(panelRightNavigationInfo.SelectedItem, prevItemIndexRight);
                                    panelRightNavigationInfo.FirstItem = 0;
                                    panelRightNavigationInfo.LastItem = 0;

                                    ClearPanelData(cursorsPanelRight);
                                    FillUpPanel(panelRightContent, cursorsPanelRight, !isLeftActive, ref panelRightNavigationInfo);
                                }
                            }
                        }
                        break;

                    // Показ информации по выбранной папке / директории
                    case ConsoleKey.Spacebar:
                        {
                            if (isLeftActive && panelLeftContent[panelLeftNavigationInfo.SelectedItem].Type == DirItemType.Folder)
                            {
                                // Если выбранный элемент панели Папка, то обнуляем значения общего количества папок, файлов и места.
                                // Используются для отображения во всплывающем окне

                                totalDirs = 0;
                                totalFiles = 0;
                                totalSpace = 0;

                                // Рисуем всплывающее окно
                                DrawPopUpMessageBorder($"Свойства папки - {panelLeftContent[panelLeftNavigationInfo.SelectedItem].Name}");

                                // Получаем количество папок, файлов и занимаемой место данной директорией
                                (totalDirs, totalFiles, totalSpace) = GetFolderInfo(panelLeftContent[panelLeftNavigationInfo.SelectedItem].Path);
                                
                                // Отображаем полученную информацию
                                ShowPopUpMessage(new string[] {$"Кол-во папок:  {totalDirs}", $"Кол-во файлов: {totalFiles}", $"Общий размер: {FileSizeToString(totalSpace)}" }) ;

                                // Устанавливаем флаги для кнопки "Закрыть" и рисуем ее
                                btnConfirm.isVisible = true;
                                btnConfirm.isActive = true;
                                
                                DrawPopUpMessageBtns(new PopUpMessageBtn[] { btnConfirm });

                                // Ожидаем подтверждения от пользователя и сбрасываем флаги кнопки
                                Console.ReadKey();
                                btnConfirm.isVisible = false;
                                btnConfirm.isActive = false;
                                
                                // Перерисовываем UI
                                DrawUI();
                                FillUpDirPanel(rootPathLeft, ref cursorsPanelLeft);
                                FillUpDirPanel(rootPathRight, ref cursorsPanelRight);
                                FillUpPanel(panelLeftContent, cursorsPanelLeft, isLeftActive, ref panelLeftNavigationInfo);
                                FillUpPanel(panelRightContent, cursorsPanelRight, !isLeftActive, ref panelRightNavigationInfo);
                            }

                            if (!isLeftActive && panelRightContent[panelRightNavigationInfo.SelectedItem].Type == DirItemType.Folder)
                            {
                                // Если выбранный элемент панели Папка, то обнуляем значения общего количества папок, файлов и места.
                                // Используются для отображения во всплывающем окне
                                totalDirs = 0;
                                totalFiles = 0;
                                totalSpace = 0;

                                // Рисуем всплывающее окно
                                DrawPopUpMessageBorder($"Свойства папки - {panelRightContent[panelRightNavigationInfo.SelectedItem].Name}");

                                // Получаем количество папок, файлов и занимаемой место данной директорией
                                (totalDirs, totalFiles, totalSpace) = GetFolderInfo(panelRightContent[panelRightNavigationInfo.SelectedItem].Path);

                                // Отображаем полученную информацию
                                ShowPopUpMessage(new string[] {$"Кол-во папок:  {totalDirs}", $"Кол-во файлов: {totalFiles}", $"Общий размер: {FileSizeToString(totalSpace)}" });

                                // Устанавливаем флаги для кнопки "Закрыть" и рисуем ее
                                btnConfirm.isVisible = true;
                                btnConfirm.isActive = true;
                                DrawPopUpMessageBtns(new PopUpMessageBtn[] { btnConfirm });

                                // Ожидаем подтверждения от пользователя и сбрасываем флаги кнопки
                                Console.ReadKey();
                                btnConfirm.isVisible = false;
                                btnConfirm.isActive = false;

                                // Перерисовываем UI
                                DrawUI();
                                FillUpDirPanel(rootPathLeft, ref cursorsPanelLeft);
                                FillUpDirPanel(rootPathRight, ref cursorsPanelRight);
                                FillUpPanel(panelLeftContent, cursorsPanelLeft, isLeftActive, ref panelLeftNavigationInfo);
                                FillUpPanel(panelRightContent, cursorsPanelRight, !isLeftActive, ref panelRightNavigationInfo);
                            }

                        }
                        break;
                   
                    default:
                        {
                            CalculateDimensions();
                            // Вызываем функцию отрисовки UI по расчитанным позициям
                            DrawUI();
                            FillUpDirPanel(rootPathLeft,ref cursorsPanelLeft);
                            FillUpDirPanel(rootPathRight,ref cursorsPanelRight);
                            FillUpPanel(panelLeftContent, cursorsPanelLeft, isLeftActive, ref panelLeftNavigationInfo);
                            FillUpPanel(panelRightContent, cursorsPanelRight, !isLeftActive, ref panelRightNavigationInfo);
                            break;
                        }
                }

                SetCursorToDefaultPosition();
                key = Console.ReadKey();
            }

            SaveApplicationSettings();
        }


        #region UI methods

        /// <summary>
        /// Получение структуры выбранной папки.
        /// Если заданный путь директория - то получаем структуру директории и возвращаем ее
        /// Если заданный путь файл - то пытаемся запустить файл, возвращаем null
        /// Во всех других случаях возаращаем массив доступных дисков
        /// Если не удалось получить структуру директории, то возвращает null 
        /// </summary>
        /// <param name="path">Путь к папке</param>
        /// <param name="panelCursors">структура содержащая начальные позиции курсоров для для заданной панели</param>
        /// <param name="rootPath">возвращает корневую директорию для новой папки</param>
        /// <returns></returns>
        public static DirectoryItem[] GetFolderData(string path, PanelCursors panelCursors, ref string rootPath)
        {
            DirectoryItem[] dirItems = null;
            int numOfItems = 0;

            // Проверяем, что такая директория существует
            if (Directory.Exists(path))
            {
                try
                {
                    // Если директория существует, то получаем информацию о ней
                    DirectoryInfo root = new DirectoryInfo(path);

                    // Если null, то значит корневая директория и надо показывать диски.
                    if (root == null)
                    {
                        DriveInfo[] drives = DriveInfo.GetDrives();

                        if (drives != null)
                        {
                            numOfItems += drives.Length;

                            dirItems = new DirectoryItem[numOfItems];

                            for (int i = 0; i < drives.Length; i++)
                            {
                                dirItems[i].Name = drives[i].Name;
                                dirItems[i].Path = drives[i].RootDirectory.FullName;
                                dirItems[i].Type = DirItemType.Drive;
                            }
                        }
                        rootPath = "";
                    }
                    else
                    {
                        // иначе показываем структуру папки

                        string[] dir = null;    // массив имен директорий
                        string[] files = null;  // массив имен файлов

                        // Получаем названия папок и файлов в директории
                        dir = Directory.GetDirectories(path);
                        files = Directory.GetFiles(path);

                        // Если в директории есть папки, то увеличиваем количество найденых элементов на количество папок
                        if (dir != null)
                        {
                            numOfItems += dir.Length;
                        }

                        // Если в директории есть файлы, то увеличиваем количество найденых элементов на количество файлов
                        if (files != null)
                        {
                            numOfItems += files.Length;
                        }


                        int offset = 1; // Индекс смещения. Изначально равен 1, т.к. первый элемент зарезервирован для перехода выше (в предыдущую папку). 

                        // Увеличиваем общее количество элементов на размер смещения.
                        numOfItems += offset;

                        // Создаем информационных массив по количеству найденых элементов
                        dirItems = new DirectoryItem[numOfItems];

                        // Если текущяя директория не является корневой, то в первом элементе запоминаем путь к папке для выхода выше
                        if (root.Parent != null)
                        {
                            dirItems[0].Name = "\\..";
                            dirItems[0].Path = root.Parent.FullName;
                            dirItems[0].Type = DirItemType.Folder;
                        }
                        else
                        {
                            // Если текущий каталог является корневым, то путь возврата будет пустым. Тип элемента - Диск
                            dirItems[0].Name = "\\..";
                            dirItems[0].Path = "";
                            dirItems[0].Type = DirItemType.Drive;
                        }

                        // Если в директории найдены папки, то заполняем информационный массив
                        if (dir != null)
                        {
                            for (int i = 0; i < dir.Length; i++)
                            {
                                DirectoryInfo dirInfo = new DirectoryInfo(dir[i]);

                                dirItems[i + offset].Name = dirInfo.Name;
                                dirItems[i + offset].Path = dirInfo.FullName;
                                dirItems[i + offset].Type = DirItemType.Folder;
                            }

                            // Увеличиваем смещение на количество папок
                            offset += dir.Length;
                        }

                        // Если в директории найдены файлы, то заполняем информационный массив
                        if (files != null)
                        {
                            for (int i = 0; i < files.Length; i++)
                            {
                                FileInfo fileInfo = new FileInfo(files[i]);

                                dirItems[i + offset].Name = fileInfo.Name;
                                dirItems[i + offset].Path = fileInfo.FullName;
                                dirItems[i + offset].Type = DirItemType.File;
                            }
                        }

                        rootPath = path;
                    }

                    // Заполняем панель отображающую путь к текущей директории
                    FillUpDirPanel(path, ref panelCursors);
                  
                    return dirItems;
                }

                catch (Exception e)
                {
                    ErrorLog(errorLogFilename, $"Ошибка доступа к директории. {e.Message}");
                    return null;
                }
            }
            else if (File.Exists(path))
            {
                try
                {
                    Process process = new Process();

                    process.StartInfo.FileName = path;
                    //process.StartInfo.UseShellExecute = false;
                    process.StartInfo.CreateNoWindow = false;
                    process.Start();
                }
                catch (Exception e)
                {
                    ErrorLog(errorLogFilename, $"Ошибка доступа к файлу. {e.Message}");
                }

                return null;
            }
            else
            {
                if (String.IsNullOrEmpty(path))
                {
                    DriveInfo[] drives = DriveInfo.GetDrives();

                    if (drives != null)
                    {
                        numOfItems += drives.Length;

                        dirItems = new DirectoryItem[numOfItems];

                        for (int i = 0; i < drives.Length; i++)
                        {
                            dirItems[i].Name = drives[i].Name;
                            dirItems[i].Path = drives[i].RootDirectory.FullName;
                            dirItems[i].Type = DirItemType.Drive;
                        }

                        // Заполняем панель отображающую путь к текущей директории
                        FillUpDirPanel("Выберите диск", ref panelCursors);
                    }

                    rootPath = "";
                }

                return dirItems;
            }
        }

        /// <summary>
        /// Заполнение панели дерева каталогов данным выбранной папки (папки, файлы и информация о них)
        /// </summary>
        /// <param name="folder">Массив элементов папки</param>
        /// <param name="panelCursors">Данные начальных положений курсоров</param>
        /// <param name="isActive">Флаг активности. True - если активна. Бедт подсвещен выбранный элемент</param>
        /// <param name="navigationInfo">Структура с данными о первом и последнем показываемом элементе списка и выбранный элемент</param>
        public static void FillUpPanel(DirectoryItem[] folder, PanelCursors panelCursors, bool isActive, ref NavigationInfo navigationInfo)
        {
            // Проверяем, что массив элементов не пустой
            if (folder != null)
            {
                FileInfo fileInfo;
                int cursorBaseY = 0;    //  Задаем смещение строк по вертикали

                // Если переданный массив элементов не пустой, то скрываем отображение курсора
                Console.CursorVisible = false;

                // Запоминаем цвет  фона консоли и цвет текста
                ConsoleColor defautlBGColor = Console.BackgroundColor;
                ConsoleColor defaultTextColor = Console.ForegroundColor;


                // Рассчитываем, какие элементы массива folder выводить в консоль
                if ((navigationInfo.FirstItem == 0 && navigationInfo.LastItem == 0) || (navigationInfo.FirstItem > navigationInfo.LastItem))
                {
                    // Если индексы первого и последнего отображаемого элемента равны 0 или индекс первого тображаемого элемента больше последнего, то
                    // проверяем, что индекс выбранного элемента меньше или равен максимальному количеству строк в панели дерева каталогов
                    if (navigationInfo.SelectedItem <= uiDimensions.panelHeight - 2)
                    {
                        // Если это так, то индекс первого отображаемого элемента равен 0
                        navigationInfo.FirstItem = 0;
                        
                        // Индекс последнего элемента либо равен количеству строк в панели, либо количеству элементов в папке, в зависимости от того, что меньше
                        navigationInfo.LastItem = folder.Length > uiDimensions.panelHeight - 2 ? uiDimensions.panelHeight - 2 : folder.Length;
                    }
                    else
                    {
                        // Если индекс выбранного элемент больше, то
                        // индекс первого отображаемого элемента равен разнице между индексом выбранного элемента и количеством строк в панели плюс 1     
                        navigationInfo.FirstItem = navigationInfo.SelectedItem - uiDimensions.panelHeight + 3;
                        // Индекс последнего элемента равен индексу выбранного элемента плюс 1
                        navigationInfo.LastItem = navigationInfo.SelectedItem + 1;
                    }
                }
                else
                {                   
                    if (navigationInfo.SelectedItem < navigationInfo.FirstItem)
                    {
                        // Если индекс выбранного элемента меньше индекса первого элемента отображения, то уменьшаем индекс первого и последнего элемента отображение на 1
                        navigationInfo.FirstItem--;
                        navigationInfo.LastItem--;
                    }
                    else if (navigationInfo.LastItem  <= navigationInfo.SelectedItem)
                    {
                        // Если индекс выбранного элемента больше или равен индексу последнего элемента отображения, то увеличиваем индекс первого и последнего элемента отображение на 1
                        navigationInfo.FirstItem++;
                        navigationInfo.LastItem++;
                    }
                }

                // Если индекс выбранного элемента оказался больше индекса последнего отображаемого элемента, то исправляем данную ситуацию
                if (navigationInfo.SelectedItem > navigationInfo.LastItem)
                {
                    navigationInfo.SelectedItem = navigationInfo.LastItem > 0 ? navigationInfo.LastItem - 1 : 0;
                }


                // Выводим элементы директории в консоль
                for (int i = navigationInfo.FirstItem; i < navigationInfo.LastItem; i++)
                {
                    // Устанавливаем курсор на начало соответсвующей строки
                    // Console.SetCursorPosition(panelCursors.panelFileNameCurX, cursorBaseY + panelCursors.panelFileNameCurY);

                    // Если индекс выводимого элемента совпадает с индексом выбранного элемента и панель активна, то меняем цвет фона для подсветки выбранного элемента
                    if (i == navigationInfo.SelectedItem && isActive)
                    {
                        Console.BackgroundColor = ConsoleColor.DarkRed;
                    }

                    // Задаем цвет текста в зависимости от типа выводимого элемента (папка или файл)
                    Console.ForegroundColor = folder[i].Type == DirItemType.File ? defaultTextColor : ConsoleColor.Cyan;


                    // Устанавливаем курсор на начало соответсвующей строки
                    Console.SetCursorPosition(panelCursors.panelFileNameCurX, cursorBaseY + panelCursors.panelFileNameCurY);

                    // Выводим в консоль имя элемента, выравнивая по левому краю
                    Console.Write($"{AlignString(folder[i].Name, uiDimensions.panelRowFileWidth - 1, AlignType.Left)}");

                    // Если текущий элемент не является диском, то
                    if (folder[i].Type != DirItemType.Drive)
                    {
                        // Получаем информацию
                        fileInfo = new FileInfo(folder[i].Path);

                        // Переводим курсор на позицию вывода информации по файлу/папке
                        Console.SetCursorPosition(panelCursors.panelFileSizeCurX, cursorBaseY + panelCursors.panelFileSizeCurY);

                        // Проверяем тип элемента
                        switch (folder[i].Type)
                        {
                            // Если это файл, то выводим в консоль его размер
                            case DirItemType.File:
                                {
                                    Console.Write($"{AlignString(FileSizeToString(fileInfo.Length), uiDimensions.panelRowInfoWidth - 1, AlignType.Right)}");
                                }
                                break;
                                // Во всех оставльных случаях выводим в консоль название < Папка >
                            default:
                                {
                                    if (i == 0)
                                    {
                                        Console.Write($"{AlignString("< НАЗАД >", uiDimensions.panelRowInfoWidth - 1, AlignType.Right)}");
                                    }
                                    else
                                    {
                                        Console.Write($"{AlignString("< ПАПКА >", uiDimensions.panelRowInfoWidth - 1, AlignType.Right)}");
                                    }
                                }
                                break;
                        }

                        // Переводим курсор на апозицию вывода даты создания элемента
                        Console.SetCursorPosition(panelCursors.panelFileDateCurX + 1, cursorBaseY + panelCursors.panelFileDateCurY);

                        // Выводим в консоль дату создания элемента
                        Console.Write($"{AlignString($"{fileInfo.CreationTime:d}", uiDimensions.panelRowInfoWidth - 1, AlignType.Right)}");


                        // Получаем атрибуты элемента
                        FileAttributes fileAttributes = fileInfo.Attributes;

                        // Если индекс элемента, выводимого в консоль > 0 ( 0 индекс зарезервирован для перехода в каталог выше),
                        // то собираем строку с атрибутами файла и выводим ее в консоль
                        if (i > 0)
                        {
                            string attrbutes = null;
                            
                            attrbutes = fileAttributes.HasFlag(FileAttributes.Archive) ? " A" : "  ";
                            attrbutes = fileAttributes.HasFlag(FileAttributes.System) ? $"{attrbutes} S" : $"{attrbutes}  ";
                            attrbutes = fileAttributes.HasFlag(FileAttributes.Hidden) ? $"{attrbutes} H" : $"{attrbutes}  ";
                            attrbutes = fileAttributes.HasFlag(FileAttributes.ReadOnly) ? $"{attrbutes} R" : $"{attrbutes}  ";
                            attrbutes = fileAttributes.HasFlag(FileAttributes.Encrypted) ? $"{attrbutes} E" : $"{attrbutes}  ";

                            Console.SetCursorPosition(panelCursors.panelFileAttrCurX + 1, cursorBaseY + panelCursors.panelFileAttrCurY);
                            Console.Write($"{AlignString(attrbutes, uiDimensions.panelRowInfoWidth - 1, AlignType.Left)}");
                        }

                        // Меняем цвета консоли на цвета по умолчанию
                        Console.BackgroundColor = defautlBGColor;
                        Console.ForegroundColor = defaultTextColor;

                        // Если индекс элемента, выводимого в консоль равен индексу выбранного элемента, то
                        // выводим информацию в консоль на панель показа пути выбранного элемента
                        if (i == navigationInfo.SelectedItem)
                        {
                            // Переводим курсор на начальную позицию панели показа пути выбранного элемента
                            Console.SetCursorPosition(panelCursors.panelFilePathCurX, panelCursors.panelFilePathCurY);
                            
                            // Очищаем ее
                            Console.Write($"{new string('\x20', uiDimensions.panelInnerWidth)}");
                            
                            // Еще раз переводим курсор на начальную позицию для этой панели  
                            Console.SetCursorPosition(panelCursors.panelFilePathCurX, panelCursors.panelFilePathCurY);

                            // Если длина пути больше ширины поля для вывода, то обрезаем и заменяем вырезанные элементы точками
                            // Если нет, то выводим полностью
                            if (fileInfo.FullName.Length > uiDimensions.panelInnerWidth)
                            {
                                Console.Write($"{fileInfo.FullName.Substring(0, 3)}..{fileInfo.FullName.Substring(fileInfo.FullName.Length - uiDimensions.panelInnerWidth + 5, uiDimensions.panelInnerWidth - 5)}");
                            }
                            else
                            {
                                Console.Write($"{fileInfo.FullName}");
                            }
                        }
                    }
                    else
                    {
                        // Если выводимый элемент является диском, то
                        // переводим курсомр на начальную позицию вывода имени
                        Console.SetCursorPosition(panelCursors.panelFileSizeCurX, cursorBaseY + panelCursors.panelFileSizeCurY);

                        if (string.IsNullOrWhiteSpace(folder[i].Path) == true)
                        {
                            // Если не указан путь для этого элемента, то просто выводим название  < ДИСК >
                            Console.Write($"{AlignString("< ДИСК  >", uiDimensions.panelRowInfoWidth - 1, AlignType.Right)}");
                        }
                        else
                        {
                            // Во всех других случаях получаем информацию о диске и выводим в консоль информайию о его размере
                            DriveInfo driveInfo = new DriveInfo(folder[i].Path);
                            Console.Write($"{AlignString(FileSizeToString(driveInfo.TotalSize), uiDimensions.panelRowInfoWidth - 1, AlignType.Right)}");
                        }
                    }

                    // Меняем цвета консоли на цвета по умолчанию
                    Console.BackgroundColor = defautlBGColor;
                    Console.ForegroundColor = defaultTextColor;

                    // увеличиваем смещение строк по вертикали, т.е. переходим на слудующую строку
                    cursorBaseY++;
                }

                // По окончании переводим курсор в позицию по-умолчанию и делаем его видимым.
                SetCursorToDefaultPosition();
                Console.CursorVisible = true;
            }
        }

        /// <summary>
        /// Заполнение панели отображение пути к текущей папке
        /// </summary>
        /// <param name="message">Сообщение, отображаемое в панели</param>
        /// <param name="panelCursors">Данные начальных положений курсоров</param>
        public static void FillUpDirPanel(string message, ref PanelCursors panelCursors)
        {
            // Очищаем панель для показа пути в интерфейсе (переводим курсор и записывем пустую строку)

            Console.SetCursorPosition(panelCursors.dirInfoCurX, panelCursors.dirInfoCurY);
            Console.Write($"{new string('\x20', uiDimensions.panelInnerWidth)}");

            // Переводим курсор на начало поля показа пути

            Console.SetCursorPosition(panelCursors.dirInfoCurX, panelCursors.dirInfoCurY);

            // Если длина пути больше размера поля, то обрезаем до размера поля.

            if (message.Length > uiDimensions.panelInnerWidth)
            {
                Console.Write(ShrinkStringMiddle(message, uiDimensions.panelInnerWidth));
                //Console.Write($"{path.Substring(0, 3)}..{path.Substring(path.Length - uiDimensions.panelInnerWidth + 5, uiDimensions.panelInnerWidth - 5)}");
            }
            else
            {
                Console.Write($"{message}");
            }

        }

        /// <summary>
        /// Возвращает или сохраняет текущий выбранный индекс дерева какталога. Если index == 0, то возвращает предыдущий из стека,
        /// иначе сохраняет индекс в стек.
        /// </summary>
        /// <param name="index">номер индекса</param>
        /// <param name="stack">стек для получения/сохранения индекса</param>
        /// <returns></returns>
        public static int UpdateSelectedIndex(int index, Stack stack)
        {
            if (index == 0)
            {
                // Если стек не нулевой, то берем значение из него, иначе возвращаем 0
                return stack.Count > 0 ? (int)stack.Pop() : 0;
            }
            else
            {
                // Сохраняем индекс в стек
                // Возвращаем индекс 0, для открывшейся папки
                stack.Push(index);
                return 0;
            }
        }

        /// <summary>
        /// Очищает информацию внутри панели дерева каталогов
        /// </summary>
        /// <param name="panelCursors">Данные начальных положений курсоров</param>
        public static void ClearPanelData(PanelCursors panelCursors)
        {
            for (int i=0; i < uiDimensions.panelHeight-2; i++)
            {
                Console.SetCursorPosition(panelCursors.panelFileNameCurX, i + panelCursors.panelFileNameCurY);
                Console.Write(new string('\x20', uiDimensions.panelRowFileWidth - 1));

                Console.SetCursorPosition(panelCursors.panelFileSizeCurX, i + panelCursors.panelFileSizeCurY);
                Console.Write(new string('\x20', uiDimensions.panelRowInfoWidth - 1));

                Console.SetCursorPosition(panelCursors.panelFileDateCurX+1, i + panelCursors.panelFileDateCurY);
                Console.Write($"{new string('\x20', uiDimensions.panelRowInfoWidth-1)}");

                Console.SetCursorPosition(panelCursors.panelFileAttrCurX+1, i + panelCursors.panelFileAttrCurY);
                Console.Write($"{new string('\x20', uiDimensions.panelRowInfoWidth-1)}");
            }
        }

        /// <summary>
        /// Рисует линию панелей заданными символами
        /// </summary>
        /// <param name="panelRowFileWidth">Ширина столбца с именами папок/файлов</param>
        /// <param name="panelRowInfoWidth">Ширина столбца дополнительной информации о папке/файле</param>
        /// <param name="leftCornerSymbol">Начальный символ</param>
        /// <param name="intersectionSymbol">Символ разделения столбцов</param>
        /// <param name="lineSymbol">Символ заполнения линии</param>
        /// <param name="rightCornerSymbol">Конечный символ</param>
        public static void DrawMainPanelBorder(int panelRowFileWidth, int panelRowInfoWidth, char leftCornerSymbol, char intersectionSymbol, char lineSymbol, char rightCornerSymbol)
        {
            string rowName = new String(lineSymbol, panelRowFileWidth);
            string rowInfo = new String(lineSymbol, panelRowInfoWidth);

            for (int i=0; i<2; i++)
            {
                Console.Write(leftCornerSymbol);
                Console.Write(rowName);
                Console.Write(intersectionSymbol);
                Console.Write(rowInfo);
                Console.Write(intersectionSymbol);
                Console.Write(rowInfo);
                Console.Write(intersectionSymbol);
                Console.Write(rowInfo);
                Console.Write(rightCornerSymbol);
                if (i == 0)
                {
                    Console.Write('\x20');
                }
                else
                {
                    Console.Write(Environment.NewLine);
                }
            }
        }

        /// <summary>
        /// Рисует линию на ширину окна используя заданные символы
        /// </summary>
        /// <param name="panelWidth">Ширина окна</param>
        /// <param name="leftCornerSymbol">Начальный символ</param>
        /// <param name="lineSymbol">Символ заполнения строки</param>
        /// <param name="rightCornerSymbol">Конечный символ</param>
        public static void DrawFullWindowLine(int panelWidth, char leftCornerSymbol, char lineSymbol, char rightCornerSymbol)
        {
            string line = new String(lineSymbol, 2 * (panelWidth + 2) + 1);
            Console.CursorLeft = 0;
            Console.Write(leftCornerSymbol);
            Console.Write(line);
            Console.Write(rightCornerSymbol);
            Console.Write(Environment.NewLine);
        }

        /// <summary>
        /// Переводит курсор на позицию по умолчанию
        /// </summary>
         public static void SetCursorToDefaultPosition()
        {
            Console.SetCursorPosition(1, uiDimensions.windowHeight + 2);
            Console.Write("> ");
        }

        /// <summary>
        /// Рассичтывает размеры UI для текущего окна и сохраняет их в структуре uiDimensions для дальнейшего использования
        /// Так же рассчитывает положения курсоров для правой и левой панелей и сохраняет их в 
        /// </summary>
        public static void CalculateDimensions()
        {
            // Сохраняем высоту и ширину окна для дальнейших вычислений
            uiDimensions.windowHeight = Console.WindowHeight % 2 > 0 ? Console.WindowHeight - 3 : Console.WindowHeight - 4;
            uiDimensions.windowWidth = Console.WindowWidth % 2 > 0 ? Console.WindowWidth - 7 : Console.WindowWidth - 8;

            if (uiDimensions.windowWidth < 80)
            {
                uiDimensions.windowWidth = 80;
            }

            // Устанавливаем размер рамки в 1 символ 
            uiDimensions.borderSize = 1;

            // Рассчитываем ширину и высоту панели отображения дерева каталогов
            uiDimensions.panelWidth = (uiDimensions.windowWidth) / 2 - 1;
            uiDimensions.panelHeight = uiDimensions.windowHeight - 7;
            uiDimensions.panelInnerWidth = uiDimensions.panelWidth - (2 * uiDimensions.borderSize);

            // Устанавливаем ширину информационных столбцов равным 12 символам  
            uiDimensions.panelRowInfoWidth = 12;

            // Устанавливаем ширину столбца отображения названия папки/файла
            uiDimensions.panelRowFileWidth = uiDimensions.panelInnerWidth - (uiDimensions.panelRowInfoWidth * 3);

            //
            // Рассчитываем начальные позиции курсоров для левой панели
            //
            // Сохраняем позицию курсора первого символа для панели отображения текущей директории
            cursorsPanelLeft.dirInfoCurX = 2;
            cursorsPanelLeft.dirInfoCurY = 2;

            // Сохраняем позицию курсора первого символа первой строки панели дерева каталогов
            cursorsPanelLeft.panelFileNameCurX = 2;
            cursorsPanelLeft.panelFileNameCurY = 5;

            // Сохраняем позицию курсора первого символа информации о размере файла/каталога
            cursorsPanelLeft.panelFileSizeCurX = uiDimensions.panelRowFileWidth + 3;
            cursorsPanelLeft.panelFileSizeCurY = cursorsPanelLeft.panelFileNameCurY;

            // Сохраняем позицию курсора первого символа информации о дате создания файла/каталога
            cursorsPanelLeft.panelFileDateCurX = cursorsPanelLeft.panelFileSizeCurX + uiDimensions.panelRowInfoWidth;
            cursorsPanelLeft.panelFileDateCurY = cursorsPanelLeft.panelFileNameCurY;

            // Сохраняем позицию курсора первого символа информации о атрибутах файла/каталога
            cursorsPanelLeft.panelFileAttrCurX = cursorsPanelLeft.panelFileDateCurX + uiDimensions.panelRowInfoWidth;
            cursorsPanelLeft.panelFileAttrCurY = cursorsPanelLeft.panelFileNameCurY;

            // Сохраняем позицию курсора первого символа информации о пути выбранного файла/каталога
            cursorsPanelLeft.panelFilePathCurX = cursorsPanelLeft.panelFileNameCurX;
            cursorsPanelLeft.panelFilePathCurY = cursorsPanelLeft.panelFileNameCurY + uiDimensions.panelHeight - 1;

            //
            // Рассчитываем начальные позиции курсоров для левой панели
            //
            // Сохраняем позицию курсора первого символа для панели отображения текущей директории
            cursorsPanelRight.dirInfoCurX = uiDimensions.panelWidth + 6;
            cursorsPanelRight.dirInfoCurY = 2;

            // Сохраняем позицию курсора первого символа первой строки панели дерева каталогов
            cursorsPanelRight.panelFileNameCurX = uiDimensions.panelWidth + 6;
            cursorsPanelRight.panelFileNameCurY = 5;

            // Сохраняем позицию курсора первого символа информации о размере файла/каталога
            cursorsPanelRight.panelFileSizeCurX = cursorsPanelRight.panelFileNameCurX + uiDimensions.panelRowFileWidth + 1;
            cursorsPanelRight.panelFileSizeCurY = cursorsPanelRight.panelFileNameCurY;

            // Сохраняем позицию курсора первого символа информации о дате создания файла/каталога
            cursorsPanelRight.panelFileDateCurX = cursorsPanelRight.panelFileSizeCurX + uiDimensions.panelRowInfoWidth;
            cursorsPanelRight.panelFileDateCurY = cursorsPanelRight.panelFileNameCurY;

            // Сохраняем позицию курсора первого символа информации о атрибутах файла/каталога
            cursorsPanelRight.panelFileAttrCurX = cursorsPanelRight.panelFileDateCurX + uiDimensions.panelRowInfoWidth;
            cursorsPanelRight.panelFileAttrCurY = cursorsPanelRight.panelFileNameCurY;

            // Сохраняем позицию курсора первого символа информации о пути выбранного файла/каталога
            cursorsPanelRight.panelFilePathCurX = cursorsPanelRight.panelFileNameCurX;
            cursorsPanelRight.panelFilePathCurY = cursorsPanelRight.panelFileNameCurY + uiDimensions.panelHeight - 1;

        }

        /// <summary>
        /// Рисует рамки интерфейса по значениям заданным в структуре UIDimension
        /// </summary>
        public static void DrawUI()
        {
            Console.Clear();
            Console.WriteLine();

            // Отрисовка рамок панели отображения пути
            DrawMainPanelBorder(uiDimensions.panelRowFileWidth, uiDimensions.panelRowInfoWidth, dlTopLeftCorner, dlHorizontalLine, dlHorizontalLine, dlTopRightCorner);
            DrawMainPanelBorder(uiDimensions.panelRowFileWidth, uiDimensions.panelRowInfoWidth, dlVerticalLine, '\x20', '\x20', dlVerticalLine);
            DrawMainPanelBorder(uiDimensions.panelRowFileWidth, uiDimensions.panelRowInfoWidth, dlBottomLeftCorner, dlHorizontalLine, dlHorizontalLine, dlBottomRightCorner);


            // Отрисовка рамки панелей дерева каталогов 
            DrawMainPanelBorder(uiDimensions.panelRowFileWidth, uiDimensions.panelRowInfoWidth, dlTopLeftCorner, dlTopMiddleCross, dlHorizontalLine, dlTopRightCorner);

            for (int i = 0; i < uiDimensions.panelHeight - 2; i++)
            {
                DrawMainPanelBorder(uiDimensions.panelRowFileWidth, uiDimensions.panelRowInfoWidth, '\u2551', '\u2502', '\x20', '\u2551');
            }

            // Отрисовка рамки отображения пути выбранного элемента 
            DrawMainPanelBorder(uiDimensions.panelRowFileWidth, uiDimensions.panelRowInfoWidth, dlVerticalLeftCross, dlBottomMiddleCross, dlHorizontalLine, dlVerticalRightCross);
            DrawMainPanelBorder(uiDimensions.panelRowFileWidth, uiDimensions.panelRowInfoWidth, dlVerticalLine, '\x20', '\x20', dlVerticalLine);
            DrawMainPanelBorder(uiDimensions.panelRowFileWidth, uiDimensions.panelRowInfoWidth, dlBottomLeftCorner, dlHorizontalLine, dlHorizontalLine, dlBottomRightCorner);

            // Отрисовка рамки с информационными кнопками 
            DrawFullWindowLine(uiDimensions.panelWidth, dlTopLeftCorner, dlHorizontalLine, dlTopRightCorner);
            DrawFullWindowLine(uiDimensions.panelWidth, dlVerticalLine, '\x20', dlVerticalLine);
            DrawFullWindowLine(uiDimensions.panelWidth, dlBottomLeftCorner, dlHorizontalLine, dlBottomRightCorner);

            Console.SetCursorPosition(1, uiDimensions.windowHeight);
            Console.Write("  F1 - Помощь | F2 - Сменить диск | F5 - Скопировать | F8 - Удалить | Space - Свойства папки | F10 - Выход |");

            Console.WriteLine();
            SetCursorToDefaultPosition();
        }

        /// <summary>
        /// Обработка взаимодействия с пользователем во время процесса копирования
        /// </summary>
        /// <param name="sourceDirItems">массив элементов папки из которой копируем</param>
        /// <param name="sourceNavInfo">навигационные данные папки из которой копируем</param>
        /// <param name="destDirItems">массив элементов папки в которую копируем</param>
        /// <param name="destNavInfo">навигационные данные папки в которую копируем</param>
        /// <param name="destPanelCursors">положения курсоров папки в которую копируем</param>
        /// <param name="destRootPath">корневой каталог папки в которую копируем</param>
        public static void UICopyProcess(
            ref DirectoryItem[] sourceDirItems,
            ref NavigationInfo sourceNavInfo,
            ref DirectoryItem[] destDirItems,
            ref NavigationInfo destNavInfo,
            ref PanelCursors destPanelCursors,
            ref string destRootPath
            )
        {

            // Проверяем, что выбранный индекс  > 0. Нулевой индекс зарезервирован для перехода в каталог родителя
            if (sourceNavInfo.SelectedItem > 0)
            {
                // Выводим всплывающее окно с названием "Копирование"
                DrawPopUpMessageBorder("Копирование");

                // Проверяем тип выбранного элемента
                if (sourceDirItems[sourceNavInfo.SelectedItem].Type == DirItemType.Folder)
                {
                    // Если папка, то выводим в окно сообщение "Копировать папку"
                    ShowPopUpMessage(new string[] {
                                            " ",
                                            $"Копировать папку {sourceDirItems[sourceNavInfo.SelectedItem].Name}",
                                            " ",
                                            $"в папку {destRootPath}",
                                        });
                }
                else if (sourceDirItems[sourceNavInfo.SelectedItem].Type == DirItemType.File)
                {
                    // Если файл, то выводим в окно сообщение "Копировать файл"
                    ShowPopUpMessage(new string[] {
                                            " ",
                                            $"Копировать файл {sourceDirItems[sourceNavInfo.SelectedItem].Name}",
                                            " ",
                                            $"в {Path.Combine(destRootPath,sourceDirItems[sourceNavInfo.SelectedItem].Name)}",
                                        });
                }

                // Создаем флаг "Показывать всплывающее окно" 
                bool showPopUp = true;

                // Устанавляиваем флаги для кнопки Отмена
                btnCancel.isVisible = true;
                btnCancel.isActive = true;

                // Устанавливаем флаги для кнопки Копировать
                btnConfirmCopy.isVisible = true;
                btnConfirmCopy.isActive = false;


                // Показываем всплывающее окно до тех пор, пока активен флаг "Показывать всплывающее окно"
                while (showPopUp)
                {
                    // Рисуем кнопки всплывающего окна
                    DrawPopUpMessageBtns(new PopUpMessageBtn[] { btnCancel, btnConfirmCopy });

                    // Ожидаем выбора пользователя
                    switch (Console.ReadKey().Key)
                    {
                        // Пользователь подтвердил выбор
                        case (ConsoleKey.Enter):
                            {
                                if (btnConfirmCopy.isActive)
                                {
                                    // Если активна кнопка "Копировать", то начинаем копировать

                                    DrawPopUpMessageBorder("Копирование");
                                    Copy(sourceDirItems[sourceNavInfo.SelectedItem].Path, destRootPath, false);

                                    // Обновляем массив элементов папки панели в которую копируем
                                    DirectoryItem[] directoryItems = GetFolderData(destRootPath, destPanelCursors, ref destRootPath);

                                    // Если удалось обновить массив элементов, то сохраняем его в массиве панели
                                    // и сбрасываем индексы навигации панели, чтобы они пересчитались
                                    if (directoryItems != null)
                                    {
                                        destDirItems = directoryItems;

                                        destNavInfo.SelectedItem = 0;
                                        destNavInfo.FirstItem = 0;
                                        destNavInfo.LastItem = 0;
                                    }
                                }
                                // Сбрасываем флаг "Показывать всплывающее окно"
                                showPopUp = false;
                            }
                            break;

                        // Перемещение между кнопками
                        case (ConsoleKey.Tab):
                        case (ConsoleKey.LeftArrow):
                        case (ConsoleKey.RightArrow):
                        case (ConsoleKey.UpArrow):
                        case (ConsoleKey.DownArrow):
                            {
                                // Изменяем активность кнопок и перерисовываем их
                                if (btnCancel.isActive)
                                {
                                    btnCancel.isActive = false;
                                    btnConfirmCopy.isActive = true;
                                }
                                else
                                {
                                    btnConfirmCopy.isActive = false;
                                    btnCancel.isActive = true;

                                }
                                DrawPopUpMessageBtns(new PopUpMessageBtn[] { btnCancel, btnConfirmCopy });
                            }
                            break;
                        // При нажатии других клавиш закрываем окно
                        default:
                            {
                                showPopUp = false;
                            }
                            break;
                    }
                }

                // Сбрасываем состояния кнопок
                btnCancel.isVisible = false;
                btnCancel.isActive = false;
                btnConfirmCopy.isVisible = false;
                btnConfirmCopy.isActive = false;

            }
        }


        /// <summary>
        /// Обработка взаимодействия с пользователем во время процесса копирования
        /// </summary>
        /// <param name="sourceDirItems">массив элементов папки из которой удаляем</param>
        /// <param name="sourceNavInfo">навигационные данные папки из которой удаляем</param>
        /// <param name="sourcePanelCursors">положения курсоров папки из которой удаляем</param>
        /// <param name="sourceRootPath">корневой каталог папки из которой удаляем</param>
        public static void UIDeleteProcess(ref DirectoryItem[] sourceDirItems, ref NavigationInfo sourceNavInfo, ref PanelCursors sourcePanelCursors, ref string sourceRootPath)
        {
            // Проверяем, что выбранный индекс  > 0. Нулевой индекс зарезервирован для перехода в каталог родителя
            if (sourceNavInfo.SelectedItem > 0)
            {
                // Выводим всплывающее окно с заголовком "Удаление"
                DrawPopUpMessageBorder("Удаление");

                // Проверяем тип выбранного элемента
                if (sourceDirItems[sourceNavInfo.SelectedItem].Type == DirItemType.Folder)
                {
                    // Если папка, то выводим в окно сообщение "Вы действительно хотите удалить папку"
                    ShowPopUpMessage(new string[] { " ", $"Вы действительно хотите удалить папку {sourceDirItems[sourceNavInfo.SelectedItem].Name}?" });
                }
                else if (sourceDirItems[sourceNavInfo.SelectedItem].Type == DirItemType.File)
                {
                    // Если файл, то выводим в окно сообщение "Вы действительно хотите удалить файл"
                    ShowPopUpMessage(new string[] { " ", $"Вы действительно хотите удалить файл {sourceDirItems[sourceNavInfo.SelectedItem].Name}?" });
                }

                // Создаем флаг "Показывать всплывающее окно" 
                bool showPopUp = true;

                // Устанавляиваем флаги для кнопки Отмена
                btnCancel.isVisible = true;
                btnCancel.isActive = true;

                // Устанавляиваем флаги для кнопки Удалить
                btnConfirmDelete.isVisible = true;
                btnConfirmDelete.isActive = false;

                // Показываем всплывающее окно до тех пор, пока активен флаг "Показывать всплывающее окно"
                while (showPopUp)
                {
                    // Рисуем кнопки всплывающего окна
                    DrawPopUpMessageBtns(new PopUpMessageBtn[] { btnCancel, btnConfirmDelete });

                    // Ожидаем выбора пользователя
                    switch (Console.ReadKey().Key)
                    {
                        // Пользователь подтвердил выбор
                        case (ConsoleKey.Enter):
                            {
                                // Если активна кнопка "Удалить", то начинаем удалять
                                if (btnConfirmDelete.isActive)
                                {
                                    DrawPopUpMessageBorder("Удаление");
                                    Delete(sourceDirItems[sourceNavInfo.SelectedItem].Path, false);

                                    // Обновляем массив элементов папки панели из которой удаляем
                                    DirectoryItem[] directoryItems = GetFolderData(sourceRootPath, sourcePanelCursors, ref sourceRootPath);

                                    // Если удалось обновить массив элементов, то сохраняем его в массиве для панели
                                    // и сбрасываем индексы навигации панели, чтобы они пересчитались
                                    if (directoryItems != null)
                                    {
                                        sourceDirItems = directoryItems;

                                        sourceNavInfo.SelectedItem = sourceNavInfo.SelectedItem > 0 ? --sourceNavInfo.SelectedItem : 0;
                                        sourceNavInfo.FirstItem = sourceNavInfo.FirstItem > 0 ? --sourceNavInfo.FirstItem : 0;
                                        sourceNavInfo.LastItem = sourceNavInfo.LastItem > 0 ? --sourceNavInfo.LastItem : 0;
                                    }
                                }
                                showPopUp = false;
                            }
                            break;

                        // Перемещение между кнопками
                        case (ConsoleKey.Tab):
                        case (ConsoleKey.LeftArrow):
                        case (ConsoleKey.RightArrow):
                        case (ConsoleKey.UpArrow):
                        case (ConsoleKey.DownArrow):
                            {
                                // Изменяем активность кнопок и перерисовываем их
                                if (btnCancel.isActive)
                                {
                                    btnCancel.isActive = false;
                                    btnConfirmDelete.isActive = true;
                                }
                                else
                                {
                                    btnConfirmDelete.isActive = false;
                                    btnCancel.isActive = true;

                                }
                                DrawPopUpMessageBtns(new PopUpMessageBtn[] { btnCancel, btnConfirmDelete });
                            }
                            break;

                        // При нажатии других клавиш закрываем окно
                        default:
                            {
                                showPopUp = false;
                            }
                            break;
                    }
                }

                // Сбрасываем состояния кнопок
                btnCancel.isVisible = false;
                btnCancel.isActive = false;
                btnConfirmDelete.isVisible = false;
                btnConfirmDelete.isActive = false;
            }
        }



        /// <summary>
        /// Вызов всплывающего окна и обработка ответа пользователя. Если пользователь согласился, метод вернет true, иначе false
        /// </summary>
        /// <param name="message">Сообщение. Каждый элемент массива выводится на новой строке</param>
        /// <param name="btnCancel">Кнопка отмены</param>
        /// <param name="btnConfirm">Кнопка подтверждения</param>
        /// <returns></returns>
        public static bool ShowPopUpQuestion(string[] message, PopUpMessageBtn btnCancel, PopUpMessageBtn btnConfirm)
        {
            bool result = false;

            if (message != null)
            {
                ShowPopUpMessage(message);
            }
            
            // Создаем флаг "Показывать всплывающее окно" 
            bool showPopUp = true;

            // Устанавляиваем флаги для кнопки Пропустить
            btnCancel.isVisible = true;
            btnCancel.isActive = true;

            // Устанавливаем флаги для кнопки Заменить
            btnConfirm.isVisible = true;
            btnConfirm.isActive = false;


            // Показываем всплывающее окно до тех пор, пока активен флаг "Показывать всплывающее окно"
            while (showPopUp)
            {
                // Рисуем кнопки всплывающего окна
                DrawPopUpMessageBtns(new PopUpMessageBtn[] { btnCancel, btnConfirm });

                // Ожидаем выбора пользователя
                switch (Console.ReadKey().Key)
                {
                    // Пользователь подтвердил выбор
                    case (ConsoleKey.Enter):
                        {
                            if (btnConfirm.isActive)
                            {
                                // Если активна кнопка "Подтвердить",
                                result = true;
                            }
                            // Сбрасываем флаг "Показывать всплывающее окно"
                            showPopUp = false;
                        }
                        break;

                    // Перемещение между кнопками
                    case (ConsoleKey.Tab):
                    case (ConsoleKey.LeftArrow):
                    case (ConsoleKey.RightArrow):
                    case (ConsoleKey.UpArrow):
                    case (ConsoleKey.DownArrow):
                        {
                            // Изменяем активность кнопок и перерисовываем их
                            if (btnCancel.isActive)
                            {
                                btnCancel.isActive = false;
                                btnConfirm.isActive = true;
                            }
                            else
                            {
                                btnConfirm.isActive = false;
                                btnCancel.isActive = true;

                            }
                        }
                        break;
                    // При нажатии других клавиш закрываем окно
                    default:
                        {
                            showPopUp = false;
                        }
                        break;
                }
            }

            // Сбрасываем состояния кнопок
            btnCancel.isVisible = false;
            btnCancel.isActive = false;
            btnConfirm.isVisible = false;
            btnConfirm.isActive = false;

            return result;
        }


        #endregion

        #region String manipulation methods

        /// <summary>
        /// Возвращает строку с размером файла, например "562 B" или "12.83 Gb"
        /// </summary>
        /// <param name="fileSize">Размер файла</param>
        /// <returns></returns>
        public static string FileSizeToString(double fileSize)
        {
            if (fileSize < 1000)
            {
                return $"{fileSize}  B";
            }
            else if (fileSize < 1000000)
            {
                return $"{(fileSize / 1024): ###.00} Kb";
            }
            else if (fileSize < 1000000000)
            {
                return $"{(fileSize / 1048576): ###.00} Mb";
            }
            else
            {
                return $"{(fileSize / 1073741824): ###.00} Gb";
            }
        }

        /// <summary>
        /// Возвращает строку заданной ширины, в которой сообщение выравнено в соответствии с заданным параметром alignType
        /// Пустое место заполняется пробелами
        /// </summary>
        /// <param name="data">Входная строка</param>
        /// <param name="width">Заданная ширина</param>
        /// <param name="alignType">Параметр выравнивания (по левому краю, по центру, по правому краю)</param>
        /// <returns></returns>
        public static string AlignString(string data, int width, AlignType alignType)
        {
            
            if (string.IsNullOrEmpty(data))
            {
                return null;
            }
            else
            {
                if (data.Length > width)
                {   
                    // Если исходная строка больше заданной ширины, то обрезаем ее до заданной ширины
                    return ShrinkStringEnd(data, width);
                }
                else if (data.Length == width)
                {
                    // Если исходная строка равна заданной ширине, то возвращаем исходную строку
                    return data;
                }
                else
                {
                    // Во всех остальных случаях выравниваем строку с соответствии с заданным параметром выравнивания, заполняя свободное место пробелами
                    switch (alignType)
                    {
                        case AlignType.Center:
                            {
                                // Выравнивание по центру
                                return $"{new String('\x20', (width - data.Length)/2)}{data}{new String('\x20', (width - data.Length)-((width - data.Length) / 2))}";
                            }
                        case AlignType.Right:
                            {
                                // Выравнивание по правому краю
                                return $"{new String('\x20', (width - data.Length))}{data}";
                            }
                        default:
                            {
                                // Во всех остальных случаях выравниваем по левому краю
                                return $"{data}{new String('\x20', width - data.Length)}";
                            }
                    }
                }
            }
        }

        /// <summary>
        /// Возвращает строку обрезанную до заданной ширины. 
        /// Берутся первые 3 символа строки, добавляются точки (от 1 до 3), остальное место заполняется последними символами переданной строки 
        /// </summary>
        /// <param name="data">Первоначальная строка</param>
        /// <param name="width">Заданная ширина итоговой строки</param>
        /// <returns></returns>
        public static string ShrinkStringMiddle(string data, int width)
        {
            if (string.IsNullOrEmpty(data))
            {
                return null;
            }
            else
            {
                // Если длина строки больше ширины поля вывода
                if (data.Length > width)
                {
                    string delimetr;

                    if (data.Length - width > 2)
                    {
                        // Если строка больше ширины поля больше чем на 2 символа
                        // то обрезаем начало (там должно быть название диска), добавляем 3 точки
                        // а далее заполнеяем оставшееся место последними символами строки

                        return $"{data.Substring(0, 3)}...{data.Remove(0, data.Length - width + 6)}";
                    }
                    else
                    {
                        // Если строка больше ширины поля больше меньше чем на 2 символа
                        // то обрезаем начало (там должно быть название диска), добавляем нужное количество точек
                        // а далее заполнеяем оставшееся место последними символами строки

                        delimetr = new string('.', data.Length - width);
                        return $"{data.Substring(0, 3)}{delimetr}{data.Remove(0, data.Length - width + delimetr.Length+3)}";
                    }
                }
                else
                {
                    return data;
                }
            }
        }

        /// <summary>
        /// Возвращает строку обрезанную до заданной ширины. 
        /// Берутся первые символа строки а в конце дорбавляются точки вместо тех символов, что не помещаются в заданную ширину 
        /// </summary>
        /// <param name="data">Первоначальная строка</param>
        /// <param name="width">Заданная ширина итоговой строки</param>
        /// <returns></returns>
        public static string ShrinkStringEnd(string data, int width)
        {
            if (string.IsNullOrEmpty(data))
            {
                return null;
            }
            else
            {
                // Если длина строки больше ширины поля вывода
                if (data.Length > width)
                {
                    return $"{data.Substring(0, width - 3)}...";
                }
                else
                {
                    return data;
                }
            }
        }
        
        #endregion

        #region Pop Up Message methods

        /// <summary>
        /// Рисует рамку всплывающего окна для вывода сообщения
        /// </summary>
        /// <param name="heading">Заголовок всплывающего окна</param>
        public static void DrawPopUpMessageBorder(string heading)
        {
            int popUpX = (uiDimensions.windowWidth / 2) > 2 ? (uiDimensions.windowWidth / 2) - 2 : 0;
            int popUpY = (uiDimensions.windowHeight / 3) > 2 ? (uiDimensions.windowHeight / 3) - 2 : 0;


            Console.CursorVisible = false;

            // Рисуем рамки заголовка окна
            Console.SetCursorPosition(popUpX / 2, popUpY);
            Console.Write(dlTopLeftCorner);
            Console.Write(new string(dlHorizontalLine, popUpX + 2));
            Console.Write(dlTopRightCorner);

            Console.SetCursorPosition(popUpX / 2, popUpY + 1);
            Console.Write(dlVerticalLine);
            Console.Write(AlignString(heading, popUpX+2, AlignType.Center));
            Console.Write(dlVerticalLine);

            Console.SetCursorPosition(popUpX / 2, popUpY + 2);
            Console.Write(dlBottomLeftCorner);
            Console.Write(new string(dlHorizontalLine, popUpX + 2));
            Console.Write(dlBottomRightCorner);

            // Рисуем тело окна, куда выводится сообщение
            for (int i = 3; i <= popUpY; i++)
            {
                Console.SetCursorPosition(popUpX / 2, popUpY + i);
                Console.Write(dlVerticalLine);
                Console.Write(new string('\x20', popUpX + 2));
                Console.Write(dlVerticalLine);
            }

            // Рисуем поле для кнопок
            Console.SetCursorPosition(popUpX / 2, popUpY + popUpY + 1);
            Console.Write(dlTopLeftCorner);
            Console.Write(new string(dlHorizontalLine, popUpX + 2));
            Console.Write(dlTopRightCorner);

            Console.SetCursorPosition(popUpX / 2, popUpY + popUpY +  2);
            Console.Write(dlVerticalLine);
            Console.Write(new string('\x20', popUpX + 2));
            Console.Write(dlVerticalLine);

            Console.SetCursorPosition(popUpX / 2, popUpY + popUpY + 3);
            Console.Write(dlVerticalLine);
            Console.Write(new string('\x20', popUpX + 2));
            Console.Write(dlVerticalLine);

            Console.SetCursorPosition(popUpX / 2, popUpY + popUpY + 4);
            Console.Write(dlVerticalLine);
            Console.Write(new string('\x20', popUpX + 2));
            Console.Write(dlVerticalLine);

            Console.SetCursorPosition(popUpX / 2, popUpY + popUpY + 5);
            Console.Write(dlBottomLeftCorner);
            Console.Write(new string(dlHorizontalLine, popUpX + 2));
            Console.Write(dlBottomRightCorner);
        }

        /// <summary>
        /// Добавляет сообщение во всплывающее окно
        /// </summary>
        /// <param name="messages">Массив строк сообщения. Каждый элемент массива выводится на новой строке</param>
        public static void ShowPopUpMessage(string[] messages)
        {
            int popUpX = (uiDimensions.windowWidth / 2) > 2 ? (uiDimensions.windowWidth / 2) - 2 : 0;
            int popUpY = (uiDimensions.windowHeight / 3) > 2 ? (uiDimensions.windowHeight / 3) - 2: 0;

            Console.CursorVisible = false;

            // Рассчитываем смещение для вывода первой строки сообщения и кол-во сообщений для вывода

            int offsetY;
            int numOfMessagesToShow;

            if (messages.Length < popUpY)
            {
                offsetY = popUpY + (popUpY - messages.Length) / 2;
                numOfMessagesToShow = messages.Length;
            }
            else
            {
                // Если количество сообщений больше чем высота тела окна, то выводим только те, что помещаются в тело окно сообщений
                // остальные сообщения не выводятся
                offsetY = popUpY;
                numOfMessagesToShow = popUpY;
            }

            // Выводим сообщение
            for (int i = 0; i < numOfMessagesToShow; i++)
            {
                Console.SetCursorPosition((popUpX / 2) + 3, offsetY + i);
                Console.Write(new String('\x20', popUpX));
                Console.SetCursorPosition((popUpX / 2) + 3, offsetY + i);
                Console.Write(AlignString(ShrinkStringMiddle(messages[i],popUpX), popUpX, AlignType.Left));           
            }

            //Console.CursorVisible = true;
            SetCursorToDefaultPosition();
        }

        /// <summary>
        /// Рисует кнопки во всплывающем окне
        /// </summary>
        /// <param name="btns">Массив кнопок для отображения. Параметры кнопок определяют способ их отображения</param>
        public static void DrawPopUpMessageBtns(PopUpMessageBtn[] btns)
        {
            if (btns != null)
            {
                // Если массив кнопок не нулевой, то рассчитываем положение курсора для их вывода

                int popUpX = (uiDimensions.windowWidth / 2) > 2 ? (uiDimensions.windowWidth / 2) - 2 : 0;
                int popUpY = (uiDimensions.windowHeight / 3) > 2 ? (uiDimensions.windowHeight / 3) - 2 : 0;
                int numOfBtns = btns.Length;

                // Запоминаме текущий цвет фона, т.к. фон у активной кнопки будет другой
                ConsoleColor defaultBGColour = Console.BackgroundColor;

                // Переводим курсор в нужную позицию и очищаем поле для кнопок
                Console.SetCursorPosition(popUpX / 2, popUpY + popUpY + 2);
                Console.Write(dlVerticalLine);
                Console.Write(new string('\x20', popUpX + 2));
                Console.Write(dlVerticalLine);

                // Опять переводим курсор в нужную позицию и выводим кнопки
                Console.SetCursorPosition(popUpX / 2, popUpY + popUpY + 2);

                for (int i = 0; i < numOfBtns; i++)
                {
                    // Если установлен параметр Отображать, то рисуем кнопку
                    if (btns[i].isVisible)
                    {
                        // Если установлены параметр Активна, то меняем цвет фона
                        if (btns[i].isActive)
                        {
                            Console.BackgroundColor = ConsoleColor.DarkGray;
                        }

                        // Рассчитываем положение курсора в зависимости от количества кнопок
                        int x = popUpX / 2 + ((i * (popUpX / numOfBtns)) + (((popUpX / numOfBtns) - btns[i].Name.Length) / 2));

                        // Переводим курсор в нужную позицию и рисуем кнопку
                        Console.SetCursorPosition(x, popUpY + popUpY + 2);
                        Console.Write($" {new string('\x20',btns[i].Name.Length)} ");
                        Console.SetCursorPosition(x, popUpY + popUpY + 3);
                        Console.Write($" {btns[i].Name} ");
                        Console.SetCursorPosition(x, popUpY + popUpY + 4);
                        Console.Write($" {new string('\x20', btns[i].Name.Length)} ");
                        Console.BackgroundColor = defaultBGColour;
                    }
                }
            }
        }

        #endregion

        #region Copy/Delete/Folder info methods

        /// <summary>
        /// Копирование папки/файла 
        /// </summary>
        /// <param name="sourcePath">откуда копировать</param>
        /// <param name="destinationPath">куда копировать</param>
        /// <param name="doReplace">если true, то принудительно перезаписывает существующие файлы/папки</param>
        public static void Copy(string sourcePath, string destinationPath, bool doReplace)
        {
            bool replace = false;

            // Проверяем, что переданные пути не являются пустыми и пути не равны друг другу (копирование папки в саму себя)
            if (string.IsNullOrWhiteSpace(sourcePath) == false && string.IsNullOrWhiteSpace(destinationPath) == false && String.Equals(sourcePath, destinationPath) == false)
            {
                // Если это так, то проверяем, что источником является файл и директория, куда его надо скопировать, тоже существует
                if (File.Exists(sourcePath) && Directory.Exists(destinationPath))
                {
                    // Добавляем к пути, куда надо скопировать, название файла.
                    string destinationFilePath = Path.Combine(destinationPath, new FileInfo(sourcePath).Name);

                    if (String.Equals(sourcePath, destinationFilePath) == false)
                    {

                        // Если файла не существует по указанному пути, то записываем во всплывающее окно информацию о копировании файла 
                        if (File.Exists(destinationFilePath) == false)
                        {
                            try
                            {
                                ShowPopUpMessage(new string[] { "Копирование:", " ", new FileInfo(sourcePath).Name, " ", "в", " ", destinationFilePath });
                                File.Copy(sourcePath, destinationFilePath);
                            }
                            catch (Exception e)
                            {
                                ShowPopUpMessage(new string[] { "Ошибка копирования файла", " ", sourcePath, " ", "в", " ", destinationFilePath, $"Ошибка: {e.Message}" });
                                Console.ReadKey();
                                ErrorLog(errorLogFilename, $"Ошибка копирования файла {sourcePath} в {destinationFilePath}. {e.Message}.");
                            }
                        }
                        else
                        {
                            // Если файл уже существует, то выводим сообщение, что такой файл уже существует

                            bool result = doReplace || ShowPopUpQuestion(new string[] { "Файл", " ", new FileInfo(sourcePath).Name, " ", "в", " ", destinationFilePath, " ", "уже существует. Заменить?" }, btnSkip, btnReplace);

                            // Если ползователь ответил Заменить, то сначала удаляем существующий файл, а затем копируем новый. 
                            if (result == true)
                            {
                                DrawPopUpMessageBorder("Копирование");

                                // Сначала удаляем старый файл
                                try
                                {
                                    File.Delete(destinationFilePath);
                                }
                                catch (Exception e)
                                {
                                    ShowPopUpMessage(new string[] { "Ошибка удаления файла", " ", destinationFilePath, $"Ошибка: {e.Message}" });
                                    Console.ReadKey();
                                    ErrorLog(errorLogFilename, $"Ошибка удаление файла {destinationFilePath}. {e.Message}.");
                                }

                                // Затем копируем новый
                                try
                                {
                                    ShowPopUpMessage(new string[] { "Копирование:", " ", new FileInfo(sourcePath).Name, " ", "в", " ", destinationFilePath });
                                    File.Copy(sourcePath, destinationFilePath);
                                }
                                catch (Exception e)
                                {
                                    ShowPopUpMessage(new string[] { "Ошибка копирования файла", " ", sourcePath, " ", "в", " ", destinationFilePath, $"Ошибка: {e.Message}" });
                                    Console.ReadKey();
                                    ErrorLog(errorLogFilename, $"Ошибка копирования файла {sourcePath} в {destinationFilePath}. {e.Message}.");
                                }
                            }
                        }
                    }
                }
                else if (Directory.Exists(sourcePath))
                {
                    // Если источником является директория, то добавляем к пути, куда надо скопировать, название директории.

                    string nextPath = Path.Combine(destinationPath, new DirectoryInfo(sourcePath).Name);

                    if (String.Equals(sourcePath, nextPath) == false)
                    {
                        // Проверяем, существует ли уже такая директория в той  папке куда копируем
                        if (Directory.Exists(nextPath) == false)
                        {
                            // Если нет, то создаем ее
                            try
                            {
                                ShowPopUpMessage(new string[] { "Копирование:", " ", new DirectoryInfo(sourcePath).Name, " ", "в", " ", destinationPath });
                                Directory.CreateDirectory(nextPath);

                                string[] dirs = null;
                                string[] files = null;

                                // Получаем список поддиректорий
                                try
                                {
                                    dirs = Directory.GetDirectories(sourcePath);
                                }
                                catch (Exception e)
                                {
                                    ShowPopUpMessage(new string[] { "Ошибка доступа к директории", " ", sourcePath, " ", $"Ошибка: {e.Message}" }); ;
                                    Console.ReadKey();
                                    ErrorLog(errorLogFilename, $"Ошибка доступа к директории {sourcePath}. {e.Message}.");
                                }

                                // Получаем список файлов в директории
                                try
                                {
                                    files = Directory.GetFiles(sourcePath);
                                }
                                catch (Exception e)
                                {
                                    ShowPopUpMessage(new string[] { "Ошибка доступа к файлу", " ", sourcePath, " ", $"Ошибка: {e.Message}" }); ;
                                    Console.ReadKey();
                                    ErrorLog(errorLogFilename, $"Ошибка доступа к файлу {sourcePath}. {e.Message}.");
                                }

                                // Если есть поддиректории, то запускаем каопирование для каждой из них
                                if (dirs != null)
                                {
                                    foreach (string dir in dirs)
                                    {
                                        Copy(dir, nextPath, false);
                                    }
                                }

                                // Если есть файлы, то запускаем копирование каждого файла
                                if (files != null)
                                {
                                    foreach (string file in files)
                                    {
                                        Copy(file, nextPath, false);
                                    }
                                }

                            }
                            catch (Exception e)
                            {
                                ShowPopUpMessage(new string[] { "Ошибка создания директории", " ", nextPath, $"Ошибка: {e.Message}" }); ;
                                Console.ReadKey();
                                ErrorLog(errorLogFilename, $"Ошибка создания директории {nextPath}. {e.Message}.");
                            }

                        }
                        else
                        {
                            // Если такая директория уже существует, то выводим сообщение о том, что такая директория уже существует

                            replace = doReplace || ShowPopUpQuestion(new string[] { "Директория", " ", new DirectoryInfo(sourcePath).Name, " ", "в", " ", destinationPath, " ", "уже существует. Заменить?" }, btnSkip, btnReplace);

                            // Если ползователь ответил Заменить, то сначала удаляем существующую папку, а затем запускаем капирование этой папки еще раз
                            if (replace == true)
                            {
                                DrawPopUpMessageBorder("Удаление");

                                try
                                {
                                    Delete(nextPath, false);
                                }
                                catch (Exception e)
                                {
                                    ShowPopUpMessage(new string[] { "Ошибка удаления директории", " ", nextPath, $"Ошибка: {e.Message}" });
                                    Console.ReadKey();
                                    ErrorLog(errorLogFilename, $"Ошибка удаление директории {nextPath}. {e.Message}.");
                                }

                                try
                                {
                                    Copy(sourcePath, destinationPath, true);
                                }
                                catch (Exception e)
                                {
                                    ShowPopUpMessage(new string[] { "Ошибка создания директории", " ", nextPath, $"Ошибка: {e.Message}" }); ;
                                    Console.ReadKey();
                                    ErrorLog(errorLogFilename, $"Ошибка копирования директории {nextPath}. {e.Message}.");
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Удаление файла или папки
        /// </summary>
        /// <param name="deletePath">Путь к папке (файлу)</param>
        /// <param name="doSilent">если true, то не выводит сообщение об удалении</param>
        public static void Delete(string deletePath, bool doSilent)
        {
            if (string.IsNullOrWhiteSpace(deletePath) == false)
            {
                // Если указанный путь является директорией
                if (Directory.Exists(deletePath))
                {
                    string[] dirs = null;
                    string[] files = null;

                    // Получаем список поддиректорий
                    try
                    {
                        dirs = Directory.GetDirectories(deletePath);
                    }
                    catch (Exception e)
                    {
                        ShowPopUpMessage(new string[] { "Ошибка доступа к директории", " ", deletePath, " ", $"Ошибка: {e.Message}" }); ;
                        Console.ReadKey();
                        ErrorLog(errorLogFilename, $"Ошибка доступа к директории {deletePath}. {e.Message}.");
                    }

                    // Получаем список файлов
                    try
                    {
                        files = Directory.GetFiles(deletePath);
                    }
                    catch (Exception e)
                    {
                        ShowPopUpMessage(new string[] { "Ошибка доступа к файлу", " ", deletePath, " ", $"Ошибка: {e.Message}" }); ;
                        Console.ReadKey();
                        ErrorLog(errorLogFilename, $"Ошибка доступа к файлу {deletePath}. {e.Message}.");
                    }

                    // Если в папке есть директории, то запускаем их удаление
                    if (dirs != null)
                    {
                        foreach (string dir in dirs)
                        {
                            Delete(dir, doSilent);
                        }
                    }

                    // Если в папке есть файлы, то запускаем их удаление
                    if (files != null)
                    {
                        foreach (string file in files)
                        {
                            Delete(file, doSilent);
                        }
                    }

                    // Показываем сообщение если не выключен "бесшумный" режим
                    if (doSilent == false)
                    {
                        ShowPopUpMessage(new string[] { "Удаление", " ", deletePath });
                    }

                    // Удаляем пустую директорию
                    try
                    {
                        Directory.Delete(deletePath);
                    }
                    catch (Exception e)
                    {
                        ShowPopUpMessage(new string[] { "Ошибка удаления директории", " ", deletePath, " ", $"Ошибка: {e.Message}" }); ;
                        Console.ReadKey();
                        ErrorLog(errorLogFilename, $"Ошибка удаления директории {deletePath}. {e.Message}.");
                    }

                }
                else if (File.Exists(deletePath))
                {
                    if (doSilent == false)
                    {
                        ShowPopUpMessage(new string[] { "Удаление", " ", deletePath });
                    }
                    try
                    {
                        File.Delete(deletePath);
                    }
                    catch (Exception e)
                    {
                        ShowPopUpMessage(new string[] { "Ошибка удаления файла", " ", deletePath, " ", $"Ошибка: {e.Message}" }); ;
                        Console.ReadKey();
                        ErrorLog(errorLogFilename, $"Ошибка удаления файла {deletePath}. {e.Message}.");
                    }
                    
                }
            }
        }

        /// <summary>
        /// Получение информации о папке. Количество подкаталогов, файлов и занимаемое место на диске
        /// </summary>
        /// <param name="path">Путь к папке</param>
        /// <returns></returns>
        public static (long, long, long) GetFolderInfo(string path)
        {
            long dirCount = 0 ;
            long filesCount = 0;
            long spaceCount = 0;

            if (string.IsNullOrWhiteSpace(path) == false)
            {
                if (Directory.Exists(path))
                {
                    long tmpDirCount;
                    long tmpFileCont;
                    long tmpSpaceCount;
                    string[] dirs = null;
                    string[] files = null;

                    try
                    {
                        dirs = Directory.GetDirectories(path);
                        dirCount += dirs.Length;
                        totalDirs += dirs.Length;

                        foreach (var dir in dirs)
                        {
                            (tmpDirCount, tmpFileCont, tmpSpaceCount) = GetFolderInfo(dir);

                            dirCount += tmpDirCount;
                            filesCount += tmpFileCont;
                            spaceCount += tmpSpaceCount;
                        }
                    }
                    catch
                    {

                    }

                    try
                    {
                        files = Directory.GetFiles(path);
                        filesCount += files.Length;
                        totalFiles += files.Length;

                        foreach (var file in files)
                        {
                            (tmpDirCount, tmpFileCont, tmpSpaceCount) = GetFolderInfo(file);

                            dirCount += tmpDirCount;
                            filesCount += tmpFileCont;
                            spaceCount += tmpSpaceCount;
                        }
                    }
                    catch
                    {

                    }
                }
                else if (File.Exists(path))
                {

                    //ShowPopUpMessage(new string[] { $"Папок: {totalDirs}", $"Файлов: {totalFiles}", $"Размер: {FileSizeToString(totalSpace)}" });

                    spaceCount = new FileInfo(path).Length;
                    totalSpace += spaceCount;
                }

                ShowPopUpMessage(new string[] { $"Кол-во папок:  {totalDirs}", $"Кол-во файлов: {totalFiles}", $"Общий размер: {FileSizeToString(totalSpace)}" });
            }

            return (dirCount, filesCount, spaceCount);
        }

        #endregion

        #region Application specific methods

        /// <summary>
        /// Считывает сохраненные данные из файла настроек приложения о размере окна (WindowHeight и WindowWidth) и
        /// передает их в SetConsoleWindowSize
        /// Если пользовательские данные отсутствуют, то берутся данны по-умолчанию (DefaultWindowHeight и DefaultWindowWidht)
        /// </summary>
        public static void InitConsoleWindowSizeAtStartup()
        {
            if (Properties.Settings.Default.WindowHeight < 80 || Properties.Settings.Default.WindowWidth < 150)
            {
                SetConsoleWindowSize(Properties.Settings.Default.DefaultWindowWidht, Properties.Settings.Default.DefaultWindowHeight);
            }
            else
            {
                SetConsoleWindowSize(Properties.Settings.Default.WindowWidth, Properties.Settings.Default.WindowHeight);
            }

        }

        /// <summary>
        /// Считывает сохраненные данные из файла настроек приложения последний путь к каталогу для правой и левой панелей
        /// </summary>
        public static void ReadSavedRootPath()
        {
            rootPathLeft = string.IsNullOrWhiteSpace(Properties.Settings.Default.LeftPanelPath) ? AppContext.BaseDirectory : Properties.Settings.Default.LeftPanelPath;
            rootPathRight = string.IsNullOrWhiteSpace(Properties.Settings.Default.RightPanelPath) ? AppContext.BaseDirectory : Properties.Settings.Default.RightPanelPath;
        }

        /// <summary>
        /// Установка размера консольного окна по заданным размерам
        /// </summary>
        /// <param name="width">ширина в символах</param>
        /// <param name="height">высота в символах</param>
        public static void SetConsoleWindowSize(int width, int height)
        {
            try
            {
                Console.SetWindowSize(width, height);
            }
            catch (Exception e)
            {
                ErrorLog(errorLogFilename, $"Ошибка при задании размера окна. Значения HxW: {height} x {width}. {e.Message}\n");
            }

        }

        /// <summary>
        /// Запись сообщений об ошибке в файл
        /// </summary>
        /// <param name="filename">путь к файлу</param>
        /// <param name="message">сообщение об ошибке</param>
        public static void ErrorLog(string filename, string message)
        {
            if (!string.IsNullOrWhiteSpace(filename) && !string.IsNullOrWhiteSpace(message))
            {
                try
                {
                    using (FileStream errorLog = new FileStream(filename, FileMode.Append))
                    {
                        byte[] buffer = Encoding.Default.GetBytes($"{Environment.NewLine}{DateTime.Now} - {message}");
                        errorLog.Write(buffer, 0, buffer.Length);
                    }
                }
                catch
                {

                }
            }
        }

        /// <summary>
        /// Сохраняет настройки приложения в файл настроек
        /// </summary>
        public static void SaveApplicationSettings()
        {
            try
            {
                // Сохраняем размеры окна
                Properties.Settings.Default.WindowHeight = Console.WindowHeight;
                Properties.Settings.Default.WindowWidth = Console.WindowWidth;
                Properties.Settings.Default.LeftPanelPath = rootPathLeft;
                Properties.Settings.Default.RightPanelPath = rootPathRight;
                Properties.Settings.Default.Save();
            }
            catch (Exception e)
            {
                ErrorLog(errorLogFilename, $"Ошибка при сохранении настроек приложения. {e.Message}\n");
            }
        }

        #endregion
    }
}
