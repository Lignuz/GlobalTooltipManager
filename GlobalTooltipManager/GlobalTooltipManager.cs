using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace Lignuz.Wpf.ToolTips
{
    /// <summary>
    /// 앱 전역에서 ToolTip의 열림/닫힘을 조율해
    /// - 컨트롤 경계 밖으로 나가면 닫고
    /// - 같은 컨트롤로 되돌아오면 시스템 지연에 맞춰 재오픈합니다.
    /// 비활성 컨트롤/경계 지터를 고려해 좌표 판정과 그레이스 타임을 사용합니다.
    /// </summary>
    public static class GlobalToolTipManager
    {
        private static ToolTip? _current;                     // 현재 화면에 떠 있는 ToolTip
        private static WeakReference? _lastHostToReopen;      // 우리가 닫았던 마지막 호스트(재오픈 대상)
        private static bool _reopenPending;                   // 재오픈 타이머 중복 방지

        private static DateTime _lastOpenedAt;                // 마지막 Open 시각
        private static readonly TimeSpan _grace = TimeSpan.FromMilliseconds(120); // Open 직후 노이즈 무시

        /// <summary>
        /// 전역 훅을 활성화합니다. App.OnStartup 등에서 한 번만 호출하세요.
        /// </summary>
        public static void Initialize() { /* static ctor 강제 실행용 */ }

        static GlobalToolTipManager()
        {
            // 실제 표시된 ToolTip 인스턴스의 라이프사이클만 추적
            EventManager.RegisterClassHandler(typeof(ToolTip), ToolTip.OpenedEvent,
                new RoutedEventHandler(OnOpened), /*handledEventsToo*/ true);
            EventManager.RegisterClassHandler(typeof(ToolTip), ToolTip.ClosedEvent,
                new RoutedEventHandler(OnClosed), true);

            // 창 단위로 전역 마우스 이동을 감시해 닫기/재오픈 트리거
            EventManager.RegisterClassHandler(typeof(Window), UIElement.PreviewMouseMoveEvent,
                new MouseEventHandler(OnPreviewMouseMove), true);
        }

        private static void OnOpened(object sender, RoutedEventArgs e)
        {
            _current = (ToolTip)sender;
            _lastHostToReopen = null; // 새로 떴으면 이전 예약은 무효
            _reopenPending = false;
            _lastOpenedAt = DateTime.UtcNow;
        }

        private static void OnClosed(object sender, RoutedEventArgs e)
        {
            if (ReferenceEquals(_current, sender))
                _current = null;
            // _lastHostToReopen은 유지(우리가 닫은 케이스에서만 셋되어 재진입 시 활용)
        }

        private static void OnPreviewMouseMove(object sender, MouseEventArgs e)
        {
            // 열림 직후 작은 흔들림(배치/측정 지터)은 무시
            if (_current is { IsOpen: true } && DateTime.UtcNow - _lastOpenedAt < _grace)
                return;

            // 1) 열려 있고, 호스트/툴팁 모두 영역 밖이면 닫고 재오픈 예약
            if (_current is { IsOpen: true } tip &&
                tip.PlacementTarget is FrameworkElement host)
            {
                bool overHost = IsPointerInside(host);
                bool overTip = IsPointerInside(tip);
                if (!overHost && !overTip)
                {
                    _lastHostToReopen = new WeakReference(host);
                    tip.IsOpen = false; // Closed에서 _current=null로 정리됨
                    return;
                }
            }

            // 2) 닫혀 있고, 직전에 닫았던 호스트 위로 다시 들어오면 재오픈 스케줄
            if (_current == null &&
                _lastHostToReopen?.Target is FrameworkElement h &&
                IsPointerInside(h))
            {
                TryScheduleReopen(h);
            }
        }

        private static void TryScheduleReopen(FrameworkElement host)
        {
            if (_reopenPending) return;
            _reopenPending = true;

            // 시스템 지연(InitialShowDelay) 존중. 즉시 뜨게 하려면 Value="0"으로 스타일에서 조정
            int delayMs = Math.Max(0, ToolTipService.GetInitialShowDelay(host));

            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(delayMs) };
            timer.Tick += (s, _) =>
            {
                timer.Stop();
                _reopenPending = false;

                // 여전히 같은 호스트 위에 있고, 현재 다른 툴팁이 떠 있지 않다면 직접 재오픈
                if (_current == null &&
                    _lastHostToReopen?.Target is FrameworkElement h2 &&
                    IsPointerInside(h2))
                {
                    OpenForHost(h2);
                }
                _lastHostToReopen = null; // 1회성
            };
            timer.Start();
        }

        private static void OpenForHost(FrameworkElement host)
        {
            if (host.ToolTip == null) return;

            // 문자열/객체만 설정되어 있던 툴팁을 인스턴스로 승격
            if (host.ToolTip is not ToolTip tip)
            {
                tip = new ToolTip { Content = host.ToolTip };
                host.ToolTip = tip;
            }

            if (tip.IsOpen) return;

            tip.PlacementTarget = host;
            tip.IsOpen = true; // 재오픈(비활성 컨트롤 포함)
        }

        // 좌표 기반 내부 판정: 비활성 컨트롤/히트테스트 특이점에도 안정적으로 동작
        private const double InsideEpsilon = 2.0; // 경계 버퍼(px)
        private static bool IsPointerInside(FrameworkElement fe)
        {
            if (fe == null || !fe.IsVisible || fe.ActualWidth <= 0 || fe.ActualHeight <= 0)
                return false;

            var p = Mouse.GetPosition(fe);
            return p.X >= -InsideEpsilon && p.Y >= -InsideEpsilon &&
                   p.X <= fe.ActualWidth + InsideEpsilon &&
                   p.Y <= fe.ActualHeight + InsideEpsilon;
        }

        /// <summary>
        /// 코드에서 현재 열려 있는 툴팁을 강제로 닫습니다.
        /// </summary>
        public static void CloseCurrent()
        {
            if (_current is { IsOpen: true })
                _current.IsOpen = false;
        }
    }
}
