using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Diagnostics;
using System.IO;
using Un4seen.Bass;
using Un4seen.Bass.Misc;
using System.Drawing;
using System.Media;
using System.ComponentModel;
using System.Text.RegularExpressions;

namespace MajdataEdit
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            if (Environment.GetCommandLineArgs().Contains("--ForceSoftwareRender"))
            {
                MessageBox.Show("正在以软件渲染模式运行\nソフトウェア・レンダリング・モードで動作\nBooting as software rendering mode.");
                RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
            }
        }
       
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            CheckAndStartView();

            TheWindow.Title = GetWindowsTitleString();

            SetWindowGoldenPosition();

            var handle = (new WindowInteropHelper(this)).Handle;
            Bass.BASS_Init(-1, 44100, BASSInit.BASS_DEVICE_CPSPEAKERS, handle);

            ReadSoundEffect();
            ReadEditorSetting();
            ReadMuriCheckSlideTime();

            chartChangeTimer.Elapsed += ChartChangeTimer_Elapsed;
            chartChangeTimer.AutoReset = false;
            currentTimeRefreshTimer.Elapsed += CurrentTimeRefreshTimer_Elapsed;
            currentTimeRefreshTimer.Start();
            clickSoundTimer.Elapsed += ClickSoundTimer_Elapsed;
            VisualEffectRefreshTimer.Elapsed += VisualEffectRefreshTimer_Elapsed;
            VisualEffectRefreshTimer.Start();
            waveStopMonitorTimer.Elapsed += WaveStopMonitorTimer_Elapsed;
            PlbHideTimer.Elapsed += PlbHideTimer_Elapsed;

            if (editorSetting.AutoCheckUpdate)
            {
                CheckUpdate(onStart: true);
            }
        }


        //start the view and wait for boot, then set window pos
        private void SetWindowPosTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            Timer setWindowPosTimer = (Timer)sender;
            Dispatcher.Invoke(() =>
            {
                InternalSwitchWindow();
            });
            setWindowPosTimer.Stop();
            setWindowPosTimer.Dispose();
        }

        // This update very freqently to Draw FFT wave.
        private void VisualEffectRefreshTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            DrawFFT();
        }
        // This update very freqently to play sound effect.
        private void ClickSoundTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            SoundEffectUpdate();
        }
        // This update less frequently. set the time text.
        private void CurrentTimeRefreshTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            UpdateTimeDisplay();
        }
        // This update "middle" frequently to monitor if the wave has to be stopped
        private void WaveStopMonitorTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            WaveStopMonitorUpdate();
        }
        /// <summary>
        /// 谱面变更延迟解析
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ChartChangeTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            Console.WriteLine("TextChanged");
            Dispatcher.Invoke(
                new Action(
                    delegate
                    {
                        SimaiProcess.Serialize(GetRawFumenText(), GetRawFumenPosition());
                        DrawWave();
                    }
                )
            );
        }

        //Window events
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!isSaved)
            {
                if (!AskSave())
                {
                    e.Cancel = true;
                    return;
                }
            }
            var process = Process.GetProcessesByName("MajdataView");
            if (process.Length > 0)
            {
                var result = MessageBox.Show(GetLocalizedString("AskCloseView"), GetLocalizedString("Attention"), MessageBoxButton.YesNo);
                if (result == MessageBoxResult.Yes)
                    process[0].Kill();
            }

            currentTimeRefreshTimer.Stop();
            VisualEffectRefreshTimer.Stop();

            soundSetting.Close();
            //if (bpmtap != null) { bpmtap.Close(); }
            //if (muriCheck != null) { muriCheck.Close(); }
            SaveSetting();

            Bass.BASS_ChannelStop(bgmStream);
            Bass.BASS_StreamFree(bgmStream);
            Bass.BASS_ChannelStop(answerStream);
            Bass.BASS_StreamFree(answerStream);
            Bass.BASS_ChannelStop(breakStream);
            Bass.BASS_StreamFree(breakStream);
            Bass.BASS_ChannelStop(judgeExStream);
            Bass.BASS_StreamFree(judgeExStream);
            Bass.BASS_ChannelStop(hanabiStream);
            Bass.BASS_StreamFree(hanabiStream);
            Bass.BASS_Stop();
            Bass.BASS_Free();
        }

        //Window grid events
        private void Grid_DragEnter(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.Move;
        }
        private void Grid_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                //Console.WriteLine(e.Data.GetData(DataFormats.FileDrop).ToString());
                if (e.Data.GetData(DataFormats.FileDrop).ToString() == "System.String[]")
                {
                    var path = ((string[])e.Data.GetData(DataFormats.FileDrop))[0];
                    if (path.ToLower().Contains("maidata.txt"))
                    {
                        if (!isSaved)
                        {
                            if (!AskSave()) return;
                        }
                        FileInfo fileInfo = new FileInfo(path);
                        initFromFile(fileInfo.DirectoryName);

                    }

                    return;
                }
            }
        }

#region MENU BARS
        private void Menu_New_Click(object sender, RoutedEventArgs e)
        {
            if (!isSaved)
            {
                if (!AskSave()) return;
            }
            var openFileDialog = new Microsoft.Win32.OpenFileDialog();
            openFileDialog.Filter = "track.mp3|track.mp3";
            if ((bool)openFileDialog.ShowDialog())
            {
                FileInfo fileInfo = new FileInfo(openFileDialog.FileName);
                CreateNewFumen(fileInfo.DirectoryName);
                initFromFile(fileInfo.DirectoryName);
            }
        }
        private void Menu_Open_Click(object sender, RoutedEventArgs e)
        {
            if (!isSaved)
            {
                if (!AskSave()) return;
            }
            var openFileDialog = new Microsoft.Win32.OpenFileDialog();
            openFileDialog.Filter = "maidata.txt|maidata.txt";
            if ((bool)openFileDialog.ShowDialog())
            {
                FileInfo fileInfo = new FileInfo(openFileDialog.FileName);
                initFromFile(fileInfo.DirectoryName);
            }
        }
        private void Menu_Save_Click(object sender, RoutedEventArgs e)
        {
            SaveFumen(true);
            SystemSounds.Beep.Play();
        }
        private void Menu_SaveAs_Click(object sender, RoutedEventArgs e)
        {

        }
        private void MirrorLeftRight_MenuItem_Click(object sender, RoutedEventArgs e)
        {
            var result = Mirror.NoteMirrorLeftRight(FumenContent.Selection.Text);
            FumenContent.Selection.Text = result;
        }
        private void MirrorUpDown_MenuItem_Click(object sender, RoutedEventArgs e)
        {
            var result = Mirror.NoteMirrorUpDown(FumenContent.Selection.Text);
            FumenContent.Selection.Text = result;
        }
        private void Mirror180_MenuItem_Click(object sender, RoutedEventArgs e)
        {
            var result = Mirror.NoteMirror180(FumenContent.Selection.Text);
            FumenContent.Selection.Text = result;
        }
        private void Mirror45_MenuItem_Click(object sender, RoutedEventArgs e)
        {
            var result = Mirror.NoteMirrorSpin45(FumenContent.Selection.Text);
            FumenContent.Selection.Text = result;
        }
        private void BPMtap_MenuItem_Click(object sender, RoutedEventArgs e)
        {
            BPMtap tap = new BPMtap();
            tap.Owner = this;
            tap.Show();
        }
        private void MenuItem_InfomationEdit_Click(object sender, RoutedEventArgs e)
        {
            var infoWindow = new Infomation();
            infoWindow.ShowDialog();
            TheWindow.Title = GetWindowsTitleString(SimaiProcess.title);
        }
        private void MenuItem_SimaiWiki_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start("https://w.atwiki.jp/simai/pages/25.html");
            //maidata.txtの譜面書式
        }
        private void MenuItem_GitHub_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start("https://github.com/LingFeng-bbben/MajdataView");
        }
        private void MenuItem_SoundSetting_Click(object sender, RoutedEventArgs e)
        {
            soundSetting = new SoundSetting();
            soundSetting.Owner = this;
            soundSetting.ShowDialog();
        }
        private void MuriCheck_Click_1(object sender, RoutedEventArgs e)
        {
            MuriCheck muriCheck = new MuriCheck();
            muriCheck.Owner = this;
            muriCheck.Show();
        }
        private void MenuItem_EditorSetting_Click(object sender, RoutedEventArgs e)
        {
            EditorSettingPanel esp = new EditorSettingPanel();
            esp.Owner = this;
            esp.ShowDialog();
        }
        private void Menu_ResetViewWindow(object sender, RoutedEventArgs e)
        {
            if (CheckAndStartView()) return;
            InternalSwitchWindow();
        }
        private void MenuFind_Click(object sender, RoutedEventArgs e)
        {
            if (FindGrid.Visibility == Visibility.Collapsed)
            {
                FindGrid.Visibility = Visibility.Visible;
                InputText.Focus();
            }
            else
                FindGrid.Visibility = Visibility.Collapsed;
        }
        private void CheckUpdate_Click(object sender, RoutedEventArgs e)
        {
            CheckUpdate();
        }


#endregion

#region 快捷键
        private void PlayAndPause_CanExecute(object sender, CanExecuteRoutedEventArgs e) //快捷键
        {
            TogglePlayAndStop();
        }
        private void StopPlaying_CanExecute(object sender, CanExecuteRoutedEventArgs e) //快捷键
        {
            TogglePlayAndPause();
        }
        private void SaveFile_Command_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            SaveFumen(true);
            SystemSounds.Beep.Play();
        }
        private void SendToView_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            TogglePlayAndStop(true);
        }
        private void IncreasePlaybackSpeed_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            if (Bass.BASS_ChannelIsActive(bgmStream) == BASSActive.BASS_ACTIVE_PLAYING) return;
            float speed = GetPlaybackSpeed();
            Console.WriteLine(speed);
            speed += 0.25f;
            PlbSpdLabel.Content = speed * 100 + "%";
            SetPlaybackSpeed(speed);
            PlbSpdAdjGrid.Visibility = Visibility.Visible;
            PlbHideTimer.Stop();
            PlbHideTimer.Start();
        }

        private void DecreasePlaybackSpeed_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            if (Bass.BASS_ChannelIsActive(bgmStream)==BASSActive.BASS_ACTIVE_PLAYING) return;
            float speed = GetPlaybackSpeed();
            Console.WriteLine(speed);
            speed -= 0.25f;
            PlbSpdLabel.Content = speed * 100 + "%";
            SetPlaybackSpeed(speed);
            PlbSpdAdjGrid.Visibility = Visibility.Visible;
            PlbHideTimer.Stop();
            PlbHideTimer.Start();
        }
        Timer PlbHideTimer = new Timer(1000);
        private void PlbHideTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            Dispatcher.Invoke(() => { PlbSpdAdjGrid.Visibility = Visibility.Collapsed; });
            ((Timer)sender).Stop();
        }

        private void FindCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            if (FindGrid.Visibility == Visibility.Collapsed)
            {
                FindGrid.Visibility = Visibility.Visible;
                InputText.Focus();
            }
            else
                FindGrid.Visibility = Visibility.Collapsed;
        }

        private void MoveToNextModify_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {

            string[] block_keyword = { @"[,/\)\}\]]([^,/]*)[,/]" , @"[,/\)\}\]](.*?)$" ,
                                    @"(\s*),\s*E\s*$", @"[\}\)\>\]](\s*),?",
                                };
            // 不支持tap双押简写
            // 规则：
            //  1.修改slide应当整条修改
            //  2.hold改为slide后可以直接tab修改时值
            //  3.尽量少的空白符（空格）
            string[] keyword = {
                                @"\(([\d,\s]*)\)", @"\{([\d,\s]*)\}", @"\<HS\*(\d*\.\d*)\>",         // (bpm) , {beat}, <HS*>
                                @"[\[\#\:](.*?)[\:\#\]]",                                           // [: , [# , #: , :]
                                @"(\s*[BDE]?[1-8]\s*)", @"(\s*C\s*)", @"(\s*[bfx\s]+)",            // 匹配tap,touch,修饰词
                                @"([1-8]\s*[zsw].*?)[\[\*]",                                      // 匹配头尾唯一对应slide
                                @"([h\-\<\>\^pqvV].*?)[\[\*]",                                   // 匹配其他slide和hold
                                @"\*(.*?)[\[\*]"                                                // 匹配同头双slide 
                                };
            // 找到最近的“块”
            var next_block = new TextRange(FumenContent.Document.ContentEnd, FumenContent.Document.ContentEnd);
            foreach (var k in block_keyword)
            {
                var range = MatchFromPosition(FumenContent.CaretPosition, k);

                // 选出最近的
                if (range != null && next_block.Start.CompareTo(range.Start) >= 0)
                {
                    // Start相同 优先选小范围
                    if (next_block.Start.CompareTo(range.Start) == 0 && next_block.End.CompareTo(range.End) <= 0)
                        continue;
                    next_block = range;
                }
            }
            var nearest_range = new TextRange(FumenContent.Document.ContentEnd, FumenContent.Document.ContentEnd);
            foreach (var k in keyword)
            {
                var range = MatchFromPosition(FumenContent.CaretPosition, k);

                // 选出最近的
                if (range != null && nearest_range.Start.CompareTo(range.Start) >= 0)
                {
                    // Start相同 优先选大范围
                    if (nearest_range.Start.CompareTo(range.Start) == 0 && nearest_range.End.CompareTo(range.End) >= 0)
                        continue;
                    nearest_range = range;
                }
            }

            // next block 为空：nearest_range不在next_block内
            if (next_block != null && next_block.End.CompareTo(nearest_range.Start) < 0)
            {
                nearest_range = next_block;
            }

            FumenContent.Selection.Select(nearest_range.Start, nearest_range.End);
        }

        // 尝试了多种方法，但对于新输入的空格，尝试获取Text将会被空格截断，
        // 猜测是RichTextBox对空格输入做了特殊处理，
        // 因此尝试通过PreviewKeyDown事件实现，成功
        TextRange MatchFromPosition(TextPointer position, string reg, LogicalDirection direct = LogicalDirection.Forward)
        {
            Console.WriteLine("reg: " + reg);

            Regex r = new Regex(reg);

            TextRange textRange = null;

            while (position != null)
            {
                if (position.CompareTo(FumenContent.Document.ContentEnd) == 0)
                {
                    break;
                }

                if (position.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.Text)
                {
                    String textRun = position.GetTextInRun(LogicalDirection.Forward);
                    Match m = r.Match(textRun);

                    if (m.Success)
                    {
                        position = position.GetPositionAtOffset(m.Groups[1].Index);
                        TextPointer nextPointer = position.GetPositionAtOffset(m.Groups[1].Length);
                        textRange = new TextRange(position, nextPointer);

                        position = position.GetPositionAtOffset(m.Groups[1].Length);

                        Console.WriteLine("| textRun:" + textRun +
                                        "\n| Matched:" + m.ToString() +
                                        "\n| mIndex:" + m.Index.ToString() +
                                        "\n| gIndex:" + m.Groups[1].Index.ToString() +
                                        "\n| gLen:" + m.Groups[1].Length.ToString() +
                                        "\n| sLen:" + textRange.End.CompareTo(textRange.Start).ToString() +
                                        "\n| Groups[1]:" + m.Groups[1] +
                                        "\n| select:" + textRange.Text +
                                        "\n| res.Start:" + textRange.Start.GetOffsetToPosition(position).ToString() +
                                        "\n| res.End:" + textRange.End.GetOffsetToPosition(position).ToString()
                                        );

                        break;
                    }
                    else
                    {
                        position = position.GetPositionAtOffset(textRun.Length);
                    }
                }
                else
                {
                    position = position.GetNextContextPosition(LogicalDirection.Forward);
                }
            }
            return textRange;
        }

#endregion

#region Left componients
        private void PlayAndPauseButton_Click(object sender, RoutedEventArgs e)
        {
            TogglePlayAndPause();
        }
        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleStop();
        }
        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            int i = LevelSelector.SelectedIndex;
            SetRawFumenText(SimaiProcess.fumens[i]);
            selectedDifficulty = i;
            LevelTextBox.Text = SimaiProcess.levels[selectedDifficulty];
            SetSavedState(true);
            SimaiProcess.Serialize(GetRawFumenText());
            DrawWave();

        }
        private void LevelTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            SetSavedState(false);
            if (selectedDifficulty == -1) return;
            SimaiProcess.levels[selectedDifficulty] = LevelTextBox.Text;
        }
        private void OffsetTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            SetSavedState(false);
            try
            {
                SimaiProcess.first = float.Parse(OffsetTextBox.Text);
                SimaiProcess.Serialize(GetRawFumenText());
                DrawWave();
            }
            catch { SimaiProcess.first = 0f; }
        }
        private void OffsetTextBox_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            var offset = float.Parse(OffsetTextBox.Text);
            offset += e.Delta > 0 ? 0.01f : -0.01f;
            OffsetTextBox.Text = offset.ToString();
        }
        private void FollowPlayCheck_Click(object sender, RoutedEventArgs e)
        {
            FumenContent.Focus();
        }
        private void Export_Button_Click(object sender, RoutedEventArgs e)
        {
            TogglePlayAndStop(true);
        }
        private void SettingLabel_MouseUp(object sender, MouseButtonEventArgs e)
        {
            // 单击设置的时候也可以进入设置界面
            EditorSettingPanel esp = new EditorSettingPanel();
            esp.Owner = this;
            esp.ShowDialog();
        }

#endregion

#region RichTextbox events
        private void FumenContent_SelectionChanged(object sender, RoutedEventArgs e)
        {
            NoteNowText.Content = "" + (
                 new TextRange(FumenContent.Document.ContentStart, FumenContent.CaretPosition).Text.
                 Replace("\r", "").Count(o => o == '\n') + 1) + " 行";
            if (Bass.BASS_ChannelIsActive(bgmStream) == BASSActive.BASS_ACTIVE_PLAYING && (bool)FollowPlayCheck.IsChecked)
                return;
            var time = SimaiProcess.Serialize(GetRawFumenText(), GetRawFumenPosition());

            //按住Ctrl，同时按下鼠标左键/上下左右方向键时，才改变进度，其他包含Ctrl的组合键不影响进度。
            if (Keyboard.Modifiers == ModifierKeys.Control && (
                Mouse.LeftButton == MouseButtonState.Pressed ||
                Keyboard.IsKeyDown(Key.Left) ||
                Keyboard.IsKeyDown(Key.Right) ||
                Keyboard.IsKeyDown(Key.Up) ||
                Keyboard.IsKeyDown(Key.Down)
            ))
            {
                if(Bass.BASS_ChannelIsActive(bgmStream) == BASSActive.BASS_ACTIVE_PLAYING)
                    TogglePause();
                SetBgmPosition(time);
            }
            //Console.WriteLine("SelectionChanged");
            SimaiProcess.ClearNoteListPlayedState();
            DrawCusor(time);
        }
        private void FumenContent_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (GetRawFumenText() == ""||isLoading) return;
            SetSavedState(false);
            if (chartChangeTimer.Interval < 33)
            {
                SimaiProcess.Serialize(GetRawFumenText(), GetRawFumenPosition());
                DrawWave();
            }
            else
            {
                chartChangeTimer.Stop();
                chartChangeTimer.Start();
            }
        }

        private void FumenContent_OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            // 按下Insert键，同时未按下任何组合键，切换覆盖模式
            if (e.Key == Key.Insert && Keyboard.Modifiers == ModifierKeys.None)
            {
                SwitchFumenOverwriteMode();
                e.Handled = true;
            }
            // 各类括号的自动补全
            // Gesture不支持Key类型，且为了拦截按键事件不被发送给RichTextDown，因此单独处理事件
            switch (e.Key)
            {
                case Key.D9:
                    if (Keyboard.Modifiers == ModifierKeys.Shift)
                    {
                        // 由于软拷贝，只能通过不变的ContentStart间接记录CaretPosition的位置
                        int distance = FumenContent.Document.ContentStart.GetOffsetToPosition(FumenContent.CaretPosition);
                        FumenContent.CaretPosition.InsertTextInRun("()");
                        FumenContent.CaretPosition = FumenContent.Document.ContentStart.GetPositionAtOffset(distance + 1);
                        e.Handled = true;
                    }
                    break;
                case Key.OemOpenBrackets:
                    if (Keyboard.Modifiers == ModifierKeys.Shift)
                    {
                        int distance = FumenContent.Document.ContentStart.GetOffsetToPosition(FumenContent.CaretPosition);
                        FumenContent.CaretPosition.InsertTextInRun("{}");
                        FumenContent.CaretPosition = FumenContent.Document.ContentStart.GetPositionAtOffset(distance + 1);
                    }
                    else
                    {
                        int distance = FumenContent.Document.ContentStart.GetOffsetToPosition(FumenContent.CaretPosition);
                        FumenContent.CaretPosition.InsertTextInRun("[:]");
                        FumenContent.CaretPosition = FumenContent.Document.ContentStart.GetPositionAtOffset(distance + 1);
                    }
                    e.Handled = true;
                    break;
                case Key.OemComma:
                    if (Keyboard.Modifiers == ModifierKeys.Shift)
                    {
                        int distance = FumenContent.Document.ContentStart.GetOffsetToPosition(FumenContent.CaretPosition);
                        FumenContent.CaretPosition.InsertTextInRun("<HS*>");
                        FumenContent.CaretPosition = FumenContent.Document.ContentStart.GetPositionAtOffset(distance + 4);
                        e.Handled = true;
                    }
                    break;
                // 输入一个纯粹的空格(Fuck RichTextBox)
                case Key.Space:
                    if (Keyboard.Modifiers == ModifierKeys.None)
                    {
                        int distance = FumenContent.Document.ContentStart.GetOffsetToPosition(FumenContent.CaretPosition);
                        FumenContent.CaretPosition.InsertTextInRun(" ");
                        FumenContent.CaretPosition = FumenContent.Document.ContentStart.GetPositionAtOffset(distance+1);
                        e.Handled = true;
                    }
                    break;
            }
        }
#endregion

#region Wave displayer
        private void WaveViewZoomIn_Click(object sender, RoutedEventArgs e)
        {
            if (zoominPower <6)
                zoominPower += 1;
            DrawWave();
            FumenContent.Focus();
        }
        private void WaveViewZoomOut_Click(object sender, RoutedEventArgs e)
        {
            if(zoominPower>1)
            zoominPower -= 1;
            DrawWave();
            FumenContent.Focus();
        }
        private void MusicWave_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            ScrollWave(e.Delta);
        }
        private void MusicWave_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            //lastMousePointX = e.GetPosition(this).X;
        }
        private void MusicWave_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                double delta = e.GetPosition(this).X - lastMousePointX;
                lastMousePointX = e.GetPosition(this).X;
                ScrollWave(delta*zoominPower*4d);
            }
            lastMousePointX = e.GetPosition(this).X;
        }
#endregion
        private void FindClose_MouseDown(object sender, MouseButtonEventArgs e)
        {
            FindGrid.Visibility = Visibility.Collapsed;
            FumenContent.Focus();
        }


    }
}
