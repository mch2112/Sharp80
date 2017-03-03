using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sharp80
{
    /// <summary>
    /// Various constants that describe the layout of the virtual screen
    /// </summary>
    internal static class ScreenMetrics
    {
        public const byte NUM_SCREEN_CHARS_X = 0x40;
        public const byte NUM_SCREEN_CHARS_Y = 0x10;
        public const ushort NUM_SCREEN_CHARS = NUM_SCREEN_CHARS_X * NUM_SCREEN_CHARS_Y;

        public const byte CHAR_PIXELS_X = 0x08;
        public const byte CHAR_PIXELS_Y = 0x18;

        public const float VIRTUAL_SCREEN_WIDTH = NUM_SCREEN_CHARS_X * CHAR_PIXELS_X;
        public const float VIRTUAL_SCREEN_HEIGHT = NUM_SCREEN_CHARS_Y * CHAR_PIXELS_Y;
        public const float VIRTUAL_SCREEN_ASPECT_RATIO = VIRTUAL_SCREEN_WIDTH / VIRTUAL_SCREEN_HEIGHT;
        public const float DISPLAY_SPACING = 10f;
        public const float ADV_INFO_WIDTH = 310f;
        public const float INFO_RECT_HEIGHT = 40f;
        public const float SPACING = 10f;
        public const float Z80WIDTH = 70f;

        public const float SCREEN_AND_ADV_INFO_ASPECT_RATIO = (VIRTUAL_SCREEN_WIDTH + DISPLAY_SPACING + ADV_INFO_WIDTH) / VIRTUAL_SCREEN_HEIGHT;

        // Windowed values
        public const float WINDOWED_HEIGHT = VIRTUAL_SCREEN_HEIGHT + 24;
        public const float WINDOWED_WIDTH_NORMAL = VIRTUAL_SCREEN_WIDTH + 48;
        public const float WINDOWED_WIDTH_ADVANCED = WINDOWED_WIDTH_NORMAL + DISPLAY_SPACING + ADV_INFO_WIDTH + 24 - 48;

        public const float WINDOWED_ASPECT_RATIO_NORMAL = WINDOWED_WIDTH_NORMAL / WINDOWED_HEIGHT;
        public const float WINDOWED_ASPECT_RATIO_ADVANCED = WINDOWED_WIDTH_ADVANCED / WINDOWED_HEIGHT;

    }
}
