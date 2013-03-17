using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace librsvgdotnet
{
    public class SVGImage : IDisposable
    {
        IntPtr _rsvg_handle;

        private static bool initialized = false;
        
        public SVGImage(string data)
        {
            if (!initialized)
            {
                g_type_init();
            }

            IntPtr temp;
            IntPtr handle = rsvg_handle_new_from_data(data, data.Length, out temp);

            if (handle == IntPtr.Zero)
            {
                if (temp != IntPtr.Zero)
                {
                    GError error = (GError)Marshal.PtrToStructure(temp, typeof(GError));
                    throw new Exception(error.message);
                }
                else
                {
                    throw new Exception();
                }
            }

            _rsvg_handle = handle;
        }

        public Bitmap Image(int w, int h, bool stretch)
        {
            IntPtr _ptr;
            RsvgDimensionData dim = new RsvgDimensionData();

            int dw = 0;
            int dh = 0;

            rsvg_handle_get_dimensions(_rsvg_handle, ref dim);

            double scaleX = ((double)w) / ((double)dim.width);
            double scaleY = ((double)h) / ((double)dim.height);

            if (stretch)
            {
                dw = w;
                dh = h;
            }
            else
            {
                double fixedScale = (scaleX < scaleY ? scaleX : scaleY);
                double fixedWidth = dim.width * fixedScale;
                double fixedHeight = dim.height * fixedScale;
                scaleX = fixedScale;
                scaleY = fixedScale;
                dw = (int)fixedWidth;
                dh = (int)fixedHeight;
            }

            // Initialize the gdk_pixbuf
            _ptr = gdk_pixbuf_new(ColorSpace.Rgb, true, 8, dw, dh);
            int Stride = gdk_pixbuf_get_rowstride(_ptr);
            int Width = dw;
            int Height = dh;

            IntPtr ptr = gdk_pixbuf_get_pixels(_ptr);
            byte[] src = new byte[Stride * Height];
            Marshal.Copy(src, 0, ptr, src.Length);

            // Create the cairo surface
            IntPtr surface = cairo_image_surface_create_for_data(gdk_pixbuf_get_pixels(_ptr), 0, Width, Height, Stride);
            IntPtr cairo = cairo_create(surface);

            // Set the scale and render the image
            cairo_scale(cairo, scaleX, scaleY);
            rsvg_handle_render_cairo(_rsvg_handle, cairo);

            // Destroy the cairo surface
            cairo_destroy(cairo);
            cairo_surface_destroy(surface);

            // Copy the gdk_pixbuf into a bitmap
            
            Bitmap bitmap = new Bitmap(Width, Height);

            //IntPtr ptr = gdk_pixbuf_get_pixels(_ptr);
            byte[] temp = new byte[Stride * Height];
            Marshal.Copy(ptr, temp, 0, temp.Length);

            BitmapData bd = bitmap.LockBits(new Rectangle(Point.Empty, bitmap.Size), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            if (bd.Stride == Stride)
            {
                Marshal.Copy(temp, 0, bd.Scan0, temp.Length);
            }
            else
            {
                for (int y = 0; y < Height; ++y)
                {
                    Marshal.Copy(temp, y * Stride, new IntPtr(bd.Scan0.ToInt64() + y * bd.Stride), Width * 4);
                }
            }

            bitmap.UnlockBits(bd);

            // Release the gdk_pixbuf
            g_object_unref(_ptr);

            return bitmap;
        }

        public void Dispose()
        {
            rsvg_handle_free(_rsvg_handle);
        }

        #region P/Invoke

        private enum ColorSpace { Rgb };

        [StructLayout(LayoutKind.Sequential)]
        struct RsvgDimensionData
        {
            public int width;
            public int height;
            public double em;
            public double ex;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        struct GError
        {
            int domain;
            int code;
            public string message;
        }

        [DllImport("librsvg-2-2.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        static extern IntPtr rsvg_handle_new_from_data(string data, int data_len, out IntPtr error);
        [DllImport("librsvg-2-2.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        static extern IntPtr rsvg_handle_new_from_file(string file_name, out IntPtr error);
        [DllImport("librsvg-2-2.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern IntPtr rsvg_handle_get_pixbuf(IntPtr handle);
        [DllImport("librsvg-2-2.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        static extern IntPtr rsvg_pixbuf_from_file_at_size(string file_name, int width, int height, out IntPtr error);
        [DllImport("librsvg-2-2.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        static extern void rsvg_handle_free(IntPtr handle);
        [DllImport("librsvg-2-2.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern void rsvg_init();
        [DllImport("librsvg-2-2.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern void rsvg_term();
        [DllImport("librsvg-2-2.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern bool rsvg_handle_render_cairo(IntPtr handle, IntPtr cairo);
        [DllImport("librsvg-2-2.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern void rsvg_handle_get_dimensions(IntPtr handle, ref RsvgDimensionData dimension_data);

        [DllImport("libcairo-2.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern IntPtr cairo_create(IntPtr target);
        [DllImport("libcairo-2.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern void cairo_destroy(IntPtr cairo);
        [DllImport("libcairo-2.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern void cairo_surface_destroy(IntPtr cairo);
        [DllImport("libcairo-2.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern void cairo_scale(IntPtr cairo, double w, double h);
        [DllImport("libcairo-2.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern IntPtr cairo_image_surface_create_for_data(IntPtr data, int format, int width, int height, int stride);

        [DllImport("libgobject-2.0-0.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern void g_type_init();
        [DllImport("libgobject-2.0-0.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern void g_object_unref(IntPtr obj);

        [DllImport("libgdk_pixbuf-2.0-0.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern ColorSpace gdk_pixbuf_get_colorspace(IntPtr pixbuf);
        [DllImport("libgdk_pixbuf-2.0-0.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern int gdk_pixbuf_get_n_channels(IntPtr pixbuf);
        [DllImport("libgdk_pixbuf-2.0-0.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern bool gdk_pixbuf_get_has_alpha(IntPtr pixbuf);
        [DllImport("libgdk_pixbuf-2.0-0.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern int gdk_pixbuf_get_bits_per_sample(IntPtr pixbuf);
        [DllImport("libgdk_pixbuf-2.0-0.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern IntPtr gdk_pixbuf_get_pixels(IntPtr pixbuf);
        [DllImport("libgdk_pixbuf-2.0-0.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern int gdk_pixbuf_get_width(IntPtr pixbuf);
        [DllImport("libgdk_pixbuf-2.0-0.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern int gdk_pixbuf_get_height(IntPtr pixbuf);
        [DllImport("libgdk_pixbuf-2.0-0.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern int gdk_pixbuf_get_rowstride(IntPtr pixbuf);
        [DllImport("libgdk_pixbuf-2.0-0.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        static extern string gdk_pixbuf_get_option(IntPtr pixbuf, string key);
        [DllImport("libgdk_pixbuf-2.0-0.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern IntPtr gdk_pixbuf_new(ColorSpace colorspace, bool has_alpha, int bits_per_sample, int width, int height);

        #endregion
    }
}
