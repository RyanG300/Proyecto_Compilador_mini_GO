using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;

namespace Mini_GO_compiler
{
    public class TextMarkerService : IBackgroundRenderer, ITextMarkerService
    {
        private readonly TextEditor editor;
        private readonly TextSegmentCollection<TextMarker> markers;

        public TextMarkerService(TextEditor editor)
        {
            this.editor = editor;
            markers = new TextSegmentCollection<TextMarker>(editor.Document);
        }

        public void Draw(TextView textView, DrawingContext drawingContext)
        {
            if (markers == null || !textView.VisualLinesValid) return;
            foreach (var marker in markers)
            {
                foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, marker))
                {
                    var geometry = new StreamGeometry();
                    using (var ctx = geometry.Open())
                    {
                        var start = rect.BottomLeft;
                        var end = rect.BottomRight;
                        ctx.BeginFigure(start, false, false);
                        for (double x = start.X; x < end.X; x += 3)
                            ctx.LineTo(new Point(x, start.Y + (Math.Sin(x / 4) * 2)), true, false);
                        ctx.LineTo(end, true, false);
                    }
                    drawingContext.DrawGeometry(null, new Pen(Brushes.Red, 1), geometry);
                }
            }
        }

        public KnownLayer Layer => KnownLayer.Selection;

        public void Create(int startOffset, int length, string message)
        {
            var marker = new TextMarker(startOffset, length, message);
            markers.Add(marker);
            editor.TextArea.TextView.InvalidateVisual();
        }

        public void RemoveAll(Predicate<ITextMarker> predicate)
        {
            foreach (var m in markers.Where(predicate.Invoke).ToList())
                markers.Remove(m);
            editor.TextArea.TextView.InvalidateVisual();
        }

        public ITextMarker Create(int startOffset, int length) => 
            new TextMarker(startOffset, length, null);

        public IEnumerable<ITextMarker> GetMarkersAtOffset(int offset) => 
            markers.Where(m => m.StartOffset <= offset && offset <= m.EndOffset);
    }

    public class TextMarker : TextSegment, ITextMarker
    {
        public TextMarker(int startOffset, int length, string message)
        {
            StartOffset = startOffset;
            Length = length;
            Message = message;
        }
        public string Message { get; set; }
        public object Tag { get; set; }
        public Color BackgroundColor { get; set; }
        public Color ForegroundColor { get; set; }
        public FontWeight? FontWeight { get; set; }
        public FontStyle? FontStyle { get; set; }
        public bool IsDeleted => !IsConnectedToCollection;
        public event EventHandler Deleted { add { } remove { } }
    }

    public interface ITextMarker { }
    
    public interface ITextMarkerService
    {
        ITextMarker Create(int startOffset, int length);
        void RemoveAll(Predicate<ITextMarker> predicate);
        IEnumerable<ITextMarker> GetMarkersAtOffset(int offset);
    }
}
