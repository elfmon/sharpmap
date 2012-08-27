// Copyright 2008-, SharpMapTeam
//
// This file is part of SharpMap.
// SharpMap is free software; you can redistribute it and/or modify
// it under the terms of the GNU Lesser General Public License as published by
// the Free Software Foundation; either version 2 of the License, or
// (at your option) any later version.
// 
// SharpMap is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Lesser General Public License for more details.

// You should have received a copy of the GNU Lesser General Public License
// along with SharpMap; if not, write to the Free Software
// Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA 

#define EnableMetafileClipboardSupport
/*
 * Note:
 * 
 * If you want to use MapBox control along with MapImage controls
 * you have to define the compile time constant 'UseMapBox' in the
 * properties dialog of this project. As a result you will have the
 * MapImage control and the MapBox control included in your SharpMap.UI
 * assembly.
 * 
 * If you want to use MapBox control as a replacement of MapImage 
 * control you have to define the compile time constant 'UseMapBoxAsMapImage'.
 * in the  * properties dialog of this project. As a result you will have a
 * MapImage control in your SharpMap.UI assembly which is actually this
 * MapBox control.
 * 
 * If you don't define any of the two compile time constants this control
 * is omitted.
 * 
 * FObermaier
 */
#if UseMapBox || UseMapBoxAsMapImage
using System;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using GeoAPI.Geometries;
using SharpMap.Layers;
using System.Drawing.Imaging;
using System.Diagnostics;

using GeoPoint = GeoAPI.Geometries.Coordinate;
using Geometry = GeoAPI.Geometries.IGeometry;
using BoundingBox = GeoAPI.Geometries.Envelope;
using System.Threading;
using Common.Logging;

namespace SharpMap.Forms
{
    /// <summary>
    /// MapBox Class - MapBox control for Windows forms
    /// </summary>
    /// <remarks>
    /// The ExtendedMapImage control adds more than basic functionality to a Windows Form, such as dynamic pan, widow zoom and data query.
    /// </remarks>
    [DesignTimeVisible(true)]
#if UseMapBoxAsMapImage
    public class MapImage : Control
#else
    public class MapBox : Control
#endif
    {

        static ILog logger = LogManager.GetLogger(typeof(MapBox));

        #region PreviewModes enumerator
        // ReSharper disable UnusedMember.Local
        /// <summary>
        /// Preview modes
        /// </summary>
        public enum PreviewModes
        {
            /// <summary>
            /// Best preview mode
            /// </summary>
            Best,
            /// <summary>
            /// Fast preview mode
            /// </summary>
            Fast
        }
        #endregion

        #region Position enumerators
        /// <summary>
        /// Horizontal alignment enumeration
        /// </summary>
        private enum XPosition
        {
            Center = 0,
            Right = 1,
            Left = -1
        }

        /// <summary>
        /// Vertical alignment enumeration
        /// </summary>
        private enum YPosition
        {
            Center = 0,
            Top = -1,
            Bottom = 1
        }
        // ReSharper restore UnusedMember.Local
        #endregion

        #region Tools enumerator
        /// <summary>
        /// Map tools enumeration
        /// </summary>
        public enum Tools
        {
            /// <summary>
            /// Pan
            /// </summary>
            Pan,
            /// <summary>
            /// Zoom in
            /// </summary>
            ZoomIn,
            /// <summary>
            /// Zoom out
            /// </summary>
            ZoomOut,

            /// <summary>
            /// Query bounding boxes for intersection
            /// </summary>
            QueryBox,

            /// <summary>
            /// Query tool
            /// </summary>
            Query = QueryBox,

            /// <summary>
            /// Attempt true intersection query on geometry
            /// </summary>
            QueryGeometry,

            /// <summary>
            /// Zoom window tool
            /// </summary>
            ZoomWindow,
            /// <summary>
            /// Define Point on Map
            /// </summary>
            DrawPoint,
            /// <summary>
            /// Define Line on Map
            /// </summary>
            DrawLine,
            /// <summary>
            /// Define Polygon on Map
            /// </summary>
            DrawPolygon,
            /// <summary>
            /// No active tool
            /// </summary>
            None
        }
        #endregion

        #region Events
        /// <summary>
        /// MouseEventtype fired from the MapImage control
        /// </summary>
        /// <param name="worldPos"></param>
        /// <param name="imagePos"></param>
        public delegate void MouseEventHandler(GeoPoint worldPos, MouseEventArgs imagePos);
        /// <summary>
        /// Fires when mouse moves over the map
        /// </summary>
        public new event MouseEventHandler MouseMove;
        /// <summary>
        /// Fires when map received a mouseclick
        /// </summary>
        public new event MouseEventHandler MouseDown;
        /// <summary>
        /// Fires when mouse is released
        /// </summary>		
        public new event MouseEventHandler MouseUp;
        /// <summary>
        /// Fired when mouse is dragging
        /// </summary>
        public event MouseEventHandler MouseDrag;

        /// <summary>
        /// Fired when the map has been refreshed
        /// </summary>
        public event EventHandler MapRefreshed;

        /// <summary>
        /// Fired when the map is about to change
        /// </summary>
        public event CancelEventHandler MapChanging;

        /// <summary>
        /// Fired when the map has been changed
        /// </summary>
        public event EventHandler MapChanged;

        /// <summary>
        /// Eventtype fired when the zoom was or are being changed
        /// </summary>
        /// <param name="zoom"></param>
        public delegate void MapZoomHandler(double zoom);
        /// <summary>
        /// Fired when the zoom value has changed
        /// </summary>
        public event MapZoomHandler MapZoomChanged;
        /// <summary>
        /// Fired when the map is being zoomed
        /// </summary>
        public event MapZoomHandler MapZooming;

        /// <summary>
        /// Eventtype fired when the map is queried
        /// </summary>
        /// <param name="data"></param>
        public delegate void MapQueryHandler(Data.FeatureDataTable data);
        /// <summary>
        /// Fired when the map is queried
        /// </summary>
        public event MapQueryHandler MapQueried;


        /// <summary>
        /// Eventtype fired when the center has changed
        /// </summary>
        /// <param name="center"></param>
        public delegate void MapCenterChangedHandler(GeoPoint center);
        /// <summary>
        /// Fired when the center of the map has changed
        /// </summary>
        public event MapCenterChangedHandler MapCenterChanged;

        /// <summary>
        /// Eventtype fired when the map tool is changed
        /// </summary>
        /// <param name="tool"></param>
        public delegate void ActiveToolChangedHandler(Tools tool);
        /// <summary>
        /// Fired when the active map tool has changed
        /// </summary>
        public event ActiveToolChangedHandler ActiveToolChanged;

        /// <summary>
        /// Eventtype fired when a new geometry has been defined
        /// </summary>
        /// <param name="geometry">New Geometry</param>
        public delegate void GeometryDefinedHandler(Geometry geometry);

        /// <summary>
        /// Fired when a new polygon has been defined
        /// </summary>
        public event GeometryDefinedHandler GeometryDefined;


        #endregion

        private static int _defaultColorIndex;

        private static readonly Color[] DefaultColors =
            new []
                {
                    Color.DarkRed,
                    Color.DarkGreen, 
                    Color.DarkBlue, 
                    Color.Orange, 
                    Color.Cyan, 
                    Color.Black, 
                    Color.Purple, 
                    Color.Yellow,
                    Color.LightBlue, 
                    Color.Fuchsia
                };
        private const float MinDragScalingBeforeRegen = 0.3333f;
        private const float MaxDragScalingBeforeRegen = 3f;
        
        private readonly ProgressBar _progressBar;

#if DEBUG
        private readonly Stopwatch _watch = new Stopwatch();
#endif

        //private bool m_IsCtrlPressed;
        private double _wheelZoomMagnitude = -2;
        private Tools _activeTool;
        private double _fineZoomFactor = 10;
        private Map _map;
        private int _queryLayerIndex;
        private Point _dragStartPoint;
        private Point _dragEndPoint;
        private Bitmap _dragImage;
        private Rectangle _rectangle = Rectangle.Empty;
        private bool _dragging;
        private readonly SolidBrush _rectangleBrush = new SolidBrush(Color.FromArgb(210, 244, 244, 244));
        private readonly Pen _rectanglePen = new Pen(Color.FromArgb(244, 244, 244), 1);
        
        private float _scaling;
        private Image _image = new Bitmap(1, 1);
        private Image _imageBackground = new Bitmap(1, 1);
        private Image _imageStatic = new Bitmap(1, 1);
        private Image _imageVariable = new Bitmap(1, 1);
        private BoundingBox _imageBoundingBox = new BoundingBox(0, 1, 0, 1);
        private int _imageGeneration = 0;

        private PreviewModes _previewMode;
        private bool _isRefreshing;
        private GeoPoint[] _pointArray;
        private bool _showProgress;
        private bool _zoomToPointer = true;
        private bool _setActiveToolNoneDuringRedraw = false;
        private bool _shiftButtonDragRectangleZoom = true;
        private bool _focusOnHover = false;
        private bool _panOnClick = true;
        private float _queryGrowFactor = 5f;

        readonly IMessageFilter _mousePreviewFilter = null;

        public static void RandomizeLayerColors(VectorLayer layer)
        {
            layer.Style.EnableOutline = true;
            layer.Style.Fill = new SolidBrush(Color.FromArgb(80, DefaultColors[_defaultColorIndex % DefaultColors.Length]));
            layer.Style.Outline = new Pen(Color.FromArgb(100, DefaultColors[(_defaultColorIndex + ((int)(DefaultColors.Length * 0.5))) % DefaultColors.Length]), 1f);
            _defaultColorIndex++;
        }        

        [Description("Define if the progress Bar is shown")]
        [Category("Appearance")]
        public bool ShowProgressUpdate
        {
            get
            {
                return _showProgress;
            }

            set
            {
                _showProgress = value;
                _progressBar.Visible = _showProgress;
            }
        }

        /// <summary>
        /// Sets whether the "go-to-cursor-on-click" feature is enabled or not (even if enabled it works only if the active tool is Pan)
        /// </summary>
        [Description("Sets whether the \"go-to-cursor-on-click\" feature is enabled or not (even if enabled it works only if the active tool is Pan)")]
        [DefaultValue(true)]
        [Category("Behavior")]
        public bool PanOnClick
        {
            get { return _panOnClick; }
            set
            {
                ActiveTool = Tools.Pan;
                _panOnClick = value;

            }
        }

        /// <summary>
        /// Sets whether the mapcontrol should automatically grab focus when mouse is hovering the control
        /// </summary>
        [Description("Sets whether the mapcontrol should automatically grab focus when mouse is hovering the control")]
        [DefaultValue(false)]
        [Category("Behavior")]
        public bool TakeFocusOnHover
        {
            get
            {
                return _focusOnHover;
            }
            set
            {
                _focusOnHover = value;
            }
        }

        /// <summary>
        /// Sets whether the mouse wheel should zoom to the pointer location
        /// </summary>
        [Description("Sets whether the mouse wheel should zoom to the pointer location")]
        [DefaultValue(true)]
        [Category("Behavior")]
        public bool ZoomToPointer
        {
            get { return _zoomToPointer; }
            set { _zoomToPointer = value; }
        }

        /// <summary>
        /// Sets ActiveTool to None (and changing cursor) while redrawing the map
        /// </summary>
        [Description("Sets ActiveTool to None (and changing cursor) while redrawing the map")]
        [DefaultValue(false)]
        [Category("Behavior")]
        public bool SetToolsNoneWhileRedrawing
        {
            get { return _setActiveToolNoneDuringRedraw; }
            set { _setActiveToolNoneDuringRedraw = value; }
        }

        /// <summary>
        /// Gets or sets the number of pixels by which a bounding box around the query point should be "grown" prior to perform the query
        /// </summary>
        /// <remarks>Does not apply when querying against boxes.</remarks>
        [Description("Gets or sets the number of pixels by which a bounding box around the query point should be \"grown\" prior to perform the query")]
        [DefaultValue(5)]
        [Category("Behavior")]
        public float QueryGrowFactor { get { return _queryGrowFactor; } 
            set
            {
                if (value < 0) value = 0;
                //if (value > 10)
                    _queryGrowFactor = value;
            }
        }


        [Description("The color of selecting rectangle.")]
        [Category("Appearance")]
        public Color SelectionBackColor
        {
            get { return _rectangleBrush.Color; }
            set
            {
                //if (value != m_RectangleBrush.Color)
                    _rectangleBrush.Color = value;
            }
        }

        [Description("The map image currently visualized.")]
        [Category("Appearance")]
        public Image Image
        {
            get
            {

                GetImagesAsyncEnd(null);
                return _image;
            }
        }

        [Description("The color of selectiong rectangle frame.")]
        [Category("Appearance")]
        public Color SelectionForeColor
        {
            get { return _rectanglePen.Color; }
            set
            {
                //if (value != m_RectanglePen.Color)
                    _rectanglePen.Color = value;
            }
        }

        [Description("The amount which a single movement of the mouse wheel zooms by. (Negative values are similar as OpenLayers/Google, positive are like ArcMap")]
        [DefaultValue(-2)]
        [Category("Behavior")]
        public double WheelZoomMagnitude
        {
            get { return _wheelZoomMagnitude; }
            set { _wheelZoomMagnitude = value; }
        }

        [Description("Mode used to create preview image while panning or zooming.")]
        [DefaultValue(PreviewModes.Best)]
        [Category("Behavior")]
        public PreviewModes PreviewMode
        {
            get { return _previewMode; }
            set
            {
                if (!_dragging)
                    _previewMode = value;
            }
        }

        [Description("The amount which the WheelZoomMagnitude is divided by " +
            "when the Control key is pressed. A number greater than 1 decreases " +
            "the zoom, and less than 1 increases it. A negative number reverses it.")]
        [DefaultValue(10)]
        [Category("Behavior")]
        public double FineZoomFactor
        {
            get { return _fineZoomFactor; }
            set { _fineZoomFactor = value; }
        }

        [Description("Enables shortcut to rectangle-zoom by holding down shift-button and drag rectangle")]
        [DefaultValue(true)]
        [Category("Behavior")]
        public bool EnableShiftButtonDragRectangleZoom
        {
            get { return _shiftButtonDragRectangleZoom; }
            set { _shiftButtonDragRectangleZoom = value; }
        }

        /// <summary>
        /// Map reference
        /// </summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Map Map
        {
            get { return _map; }
            set
            {
                if (value != _map)
                {
                    var cea = new CancelEventArgs(false);
                    OnMapChanging(cea);
                    if (cea.Cancel) return;

                    _map = value;

                    OnMapChanged(EventArgs.Empty);
                }
            }
        }

        /// <summary>
        /// Gets or sets the index of the active query layer 
        /// </summary>
        public int QueryLayerIndex
        {
            get { return _queryLayerIndex; }
            set { _queryLayerIndex = value; }
        }

        /// <summary>
        /// Sets the active map tool
        /// </summary>
        public Tools ActiveTool
        {
            get { return _activeTool; }
            set
            {
                var check = (value != _activeTool);

                _activeTool = value;

                SetCursor();

                _pointArray = null;

                if (check && ActiveToolChanged != null)
                    ActiveToolChanged(value);
            }
        }

#pragma warning disable 1587
#if DEBUG
        /// <summary>
        /// TimeSpan for refreshing maps
        /// </summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public TimeSpan LastRefreshTime { get; set; }
#endif

 #if UseMapBoxAsMapImage
        /// <summary>
        /// Initializes a new map
        /// </summary>
       public MapImage()
#else
        /// <summary>
        /// Initializes a new map
        /// </summary>
        public MapBox()
#endif
#pragma warning restore 1587
        {
            
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            base.DoubleBuffered = true;
            _map = new Map(ClientSize);
            VariableLayerCollection.VariableLayerCollectionRequery += HandleVariableLayersRequery;
            _map.MapNewTileAvaliable += HandleMapNewTileAvaliable;

            _activeTool = Tools.None;
            LostFocus += HandleMapBoxLostFocus;


            _progressBar = new ProgressBar
                               {
                                   Style = ProgressBarStyle.Marquee,
                                   Location = new Point(2, 2),
                                   Size = new Size(50, 10)
                               };
            Controls.Add(_progressBar);
            _progressBar.Visible = false;

            _mousePreviewFilter = new MouseWheelGrabber(this);
            Application.AddMessageFilter(_mousePreviewFilter);
            this.SizeChanged += new EventHandler(MapBox_SizeChanged);

        }

        void MapBox_SizeChanged(object sender, EventArgs e)
        {
            if (Map != null)
            {
                if (this.Size != Map.Size)
                {
                    Map.Size = this.Size;
                    Refresh();
                }
            }
        }

        volatile bool isDisposed = false;
        protected override void Dispose(bool disposing)
        {
            if (isDisposed || IsDisposed)
                return;

            VariableLayerCollection.VariableLayerCollectionRequery -= HandleVariableLayersRequery;
            if (_map != null)
            {
                _map.MapNewTileAvaliable -= HandleMapNewTileAvaliable;
            }
            LostFocus -= HandleMapBoxLostFocus;

            if (_mousePreviewFilter != null)
                Application.RemoveMessageFilter(_mousePreviewFilter);

            lock (maplocker)
            {
                _map = null;

                if (_imageStatic != null)
                {
                    _imageStatic.Dispose();
                    _imageStatic = null;
                }
                if (_imageBackground != null)
                {
                    _imageBackground.Dispose();
                    _imageBackground = null;
                }
                if (_imageVariable != null)
                {
                    _imageVariable.Dispose();
                    _imageVariable = null;
                }
                if (_image != null)
                {
                    _image.Dispose();
                    _image = null;
                }

                if (_dragImage != null)
                {
                    _dragImage.Dispose();
                    _dragImage = null;
                }

                if (_rectanglePen != null)
                {
                    _rectanglePen.Dispose();
                }
                if (_rectangleBrush != null)
                {
                    _rectangleBrush.Dispose();
                }

                base.Dispose(disposing);
                isDisposed = true;
            }
        }

        #region event handling
        /// <summary>
        /// Handles LostFocus event
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void HandleMapBoxLostFocus(object sender, EventArgs e)
        {
            if (!_dragging) return;

            _dragging = false;
            Invalidate(ClientRectangle);
        }

        /// <summary>
        /// Handles need to requery of variable layers
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void HandleVariableLayersRequery(object sender, EventArgs e)
        {
            if (IsDisposed || isDisposed)
                return;

            Image oldRef;
            lock (maplocker)
            {
                if (_dragging) return;
                oldRef = _imageVariable;
                _imageVariable = GetMap(_map,_map.VariableLayers, LayerCollectionType.Variable, _map.Envelope);
            }

            UpdateImage(false);
            if (oldRef != null)
                oldRef.Dispose();

            Invalidate();
            Application.DoEvents();
        }

        void HandleMapNewTileAvaliable(TileLayer sender, BoundingBox box, Bitmap bm, int sourceWidth, int sourceHeight, ImageAttributes imageAttributes)
        {
            lock (lockerBackgroundImages)
            {
                try
                {
                    var min = Point.Round(_map.WorldToImage(box.Min()));
                    var max = Point.Round(_map.WorldToImage(box.Max()));

                    if (IsDisposed == false && isDisposed == false)
                    {

                        using (var g = Graphics.FromImage(_imageBackground))
                        {
                            
                            g.DrawImage(bm,
                                        new Rectangle(min.X, max.Y, (max.X - min.X), (min.Y - max.Y)), 
                                        0, 0,
                                        sourceWidth, sourceHeight,
                                        GraphicsUnit.Pixel,
                                        imageAttributes);

                        }
                        
                        UpdateImage(false);
                    }
                }
                catch (Exception ex)
                {
                    logger.Warn(ex.Message, ex);
                    //this can be a GDI+ Hell Exception...
                }

            }

        }

        #endregion

        private Image GetMap(Map map, LayerCollection layers, LayerCollectionType layerCollectionType, BoundingBox extent)
        {
            try
            {
                int width = Width;
                int height = Height;

                if ((layers == null || layers.Count == 0 || width <= 0 || height <= 0))
                {
                    if (layerCollectionType == LayerCollectionType.Background)
                        return new Bitmap(1, 1);
                    return null;
                }

                var retval = new Bitmap(width, height);

                using (var g = Graphics.FromImage(retval))
                {
                    map.RenderMap(g, layerCollectionType, false);
                }

                if (layerCollectionType == LayerCollectionType.Variable)
                    retval.MakeTransparent(_map.BackColor);

                return retval;
            }
            catch (Exception ee)
            {
                logger.Error("Error while rendering map", ee);

                if (layerCollectionType == LayerCollectionType.Background)
                    return new Bitmap(1, 1);
                return null;
            }
        }


        private object lockerStaticImages = new object();
        private object lockerBackgroundImages = new object();
        private object lockerPaintImage = new object();
        private object maplocker = new object();
        private void GetImagesAsync(BoundingBox extent, int imageGeneration)
        {
            Map safeMap = null;
            lock (maplocker)
            {
                if (isDisposed)
                    return;

                if (imageGeneration < _imageGeneration)
                {
                    /*we're to old*/
                    return;
                }
                safeMap = _map.Clone();
                _imageVariable = GetMap(safeMap, _map.VariableLayers, LayerCollectionType.Variable, extent);
                lock (lockerStaticImages)
                {
                    _imageStatic = GetMap(safeMap, _map.Layers, LayerCollectionType.Static, extent);
                }
                lock (lockerBackgroundImages)
                {
                    _imageBackground = GetMap(safeMap, _map.BackgroundLayer, LayerCollectionType.Background, extent);
                }
            }
        }

        class GetImageEndResult
        {
            public Tools? Tool { get; set; }
            public BoundingBox bbox { get; set; }
            public int generation { get; set; }
        }
        private void GetImagesAsyncEnd(GetImageEndResult res)
        {
            //draw only if generation is larger than the current, else we have aldready drawn something newer
            if (res == null || res.generation < _imageGeneration || isDisposed)
                return;


            if (logger.IsDebugEnabled)
                logger.DebugFormat("{2}: {0} - {1}", res.generation, res.bbox, DateTime.Now);


            if ((_setActiveToolNoneDuringRedraw || ShowProgressUpdate) && InvokeRequired)
            {
                try
                {
                    BeginInvoke(new MethodInvoker(delegate
                    {
                        GetImagesAsyncEnd(res);
                    }));

                }
                catch (Exception ex)
                {
                    logger.Warn(ex.Message, ex);
                }
            }
            else
            {
                try
                {
                    var oldRef = _image;
                    if (Width > 0 && Height > 0)
                    {

                        var bmp = new Bitmap(Width, Height);


                        using (var g = Graphics.FromImage(bmp))
                        {
                            lock (lockerBackgroundImages)
                            {
                                //Draws the background Image
                                if (_imageBackground != null)
                                {
                                    try
                                    {
                                        g.DrawImageUnscaled(_imageBackground, 0, 0);
                                    }
                                    catch (Exception ex)
                                    {
                                        logger.Warn(ex.Message, ex);
                                    }
                                }
                            }

                            //Draws the static images
                            if (lockerStaticImages != null)
                            {
                                try
                                {
                                    if (_imageStatic != null)
                                    {
                                        g.DrawImageUnscaled(_imageStatic, 0, 0);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    logger.Warn(ex.Message, ex);
                                }

                            }

                            //Draws the variable Images
                            if (_imageVariable != null)
                            {
                                try
                                {
                                    g.DrawImageUnscaled(_imageVariable, 0, 0);
                                }
                                catch (Exception ex)
                                {
                                    logger.Warn(ex.Message, ex);
                                }
                            }

                            g.Dispose();
                        }


                        lock (lockerPaintImage)
                        {
                            _image = bmp;
                            _imageBoundingBox = res.bbox;
                        }
                    }

                    if (res.Tool.HasValue)
                    {
                        if (_setActiveToolNoneDuringRedraw)
                            ActiveTool = res.Tool.Value;

                        _dragEndPoint = new Point(0, 0);
                        _isRefreshing = false;

                        if (_setActiveToolNoneDuringRedraw)
                            Enabled = true;

                        if (ShowProgressUpdate)
                        {
                            _progressBar.Enabled = false;
                            _progressBar.Visible = false;
                        }
                    }

                    lock (lockerPaintImage)
                    {
                        if (oldRef != null)
                            oldRef.Dispose();
                    }

                    Invalidate();
                }
                catch (Exception ex)
                {
                    logger.Warn(ex.Message, ex);
                }

#if DEBUG
                _watch.Stop();
                LastRefreshTime = _watch.Elapsed;
#endif

                try
                {
                    if (MapRefreshed != null)
                    {
                        MapRefreshed(this, null);
                    }
                }
                catch (Exception ee)
                {
                    //Trap errors that occured when calling the eventhandlers
                    logger.Warn("Exception while calling eventhandler", ee);
                }
            }
        }

        private void UpdateImage(bool forceRefresh)
        {
            if (isDisposed || IsDisposed)
                return;

            if (((_imageStatic == null && _imageVariable == null && _imageBackground == null) && !forceRefresh) ||
                (Width == 0 || Height == 0)) return;

            BoundingBox bbox = _map.Envelope;
            if (forceRefresh) // && _isRefreshing == false)
            {
                _isRefreshing = true;
                Tools oldTool = ActiveTool;
                if (_setActiveToolNoneDuringRedraw)
                {
                    ActiveTool = Tools.None;
                    Enabled = false;
                }

                if (ShowProgressUpdate)
                {
                    _progressBar.Visible = true;
                    _progressBar.Enabled = true;
                }

                int generation = ++_imageGeneration;
                ThreadPool.QueueUserWorkItem(
                    delegate
                    {
                        GetImagesAsync(bbox, generation);
                        GetImagesAsyncEnd(new GetImageEndResult {Tool = oldTool, bbox= bbox, generation = generation });
                    });
            }
            else
            {
                GetImagesAsyncEnd(new GetImageEndResult {Tool=null, bbox = bbox, generation = _imageGeneration });
            }
        }


        private void SetCursor()
        {
            if (InvokeRequired)
            {
                BeginInvoke(new MethodInvoker(SetCursor));
                return;
            }

            if (_activeTool == Tools.None)
                Cursor = Cursors.Default;
            if (_activeTool == Tools.Pan)
                Cursor = Cursors.Hand;
            else if (_activeTool == Tools.Query || _activeTool == Tools.QueryGeometry)
                Cursor = Cursors.Help;
            else if (_activeTool == Tools.ZoomIn || _activeTool == Tools.ZoomOut || _activeTool == Tools.ZoomWindow)
                Cursor = Cursors.Cross;
            else if (_activeTool == Tools.DrawPoint || _activeTool == Tools.DrawPolygon || _activeTool == Tools.DrawLine)
                Cursor = Cursors.Cross;
        }


        /// <summary>
        /// Refreshes the map
        /// </summary>
        public override void Refresh()
        {
            try
            {
                //Protect against cross-thread operations...
                //We need this since we're modifying the cursor
                if (this.InvokeRequired)
                {
                    this.Invoke(new MethodInvoker(Refresh));
                    return;
                }
#if DEBUG
                _watch.Reset();
                _watch.Start();
#endif
                if (_map != null)
                {
                    _map.Size = ClientSize;
                    if ((_map.Layers == null || _map.Layers.Count == 0) &&
                        (_map.BackgroundLayer == null || _map.BackgroundLayer.Count == 0) &&
                        (_map.VariableLayers == null || _map.VariableLayers.Count == 0))
                        _image = null;
                    else
                    {
                        Cursor c = Cursor;
                        if (_setActiveToolNoneDuringRedraw)
                        {
                            Cursor = Cursors.WaitCursor;
                        }
                        UpdateImage(true);
                        if (_setActiveToolNoneDuringRedraw)
                        {
                            Cursor = c;
                        }
                    }

                    base.Refresh();
                    Invalidate();

                }
            }
            catch (Exception ex)
            {
                logger.Warn(ex.Message, ex);
            }
        }


        private static Boolean IsControlPressed { get { return (ModifierKeys & Keys.ControlKey) == Keys.ControlKey; } }


        protected override void OnMouseHover(EventArgs e)
        {
            if (_focusOnHover)
                TestAndGrabFocus();
            base.OnMouseHover(e);           
        }

        private void TestAndGrabFocus()
        {
            if (!Focused)
            {
                bool isFocused = Focus();
                logger.Debug("Focused: " + isFocused);
            }
        }

        System.Timers.Timer mouseWheelRefreshTimer;

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);

            if (_map != null)
            {
                if (_zoomToPointer)
                    _map.Center = _map.ImageToWorld(new System.Drawing.Point(e.X, e.Y), true);

                double scale = (e.Delta / 120.0);
                double scaleBase = 1 + (_wheelZoomMagnitude / (10 * (IsControlPressed ? _fineZoomFactor : 1)));

                _map.Zoom *= Math.Pow(scaleBase, scale);

                if (_zoomToPointer)
                {
                    int NewCenterX = (this.Width / 2) + ((this.Width / 2) - e.X);
                    int NewCenterY = (this.Height / 2) + ((this.Height / 2) - e.Y);

                    _map.Center = _map.ImageToWorld(new System.Drawing.Point(NewCenterX, NewCenterY), true);

                    if (MapCenterChanged != null)
                        MapCenterChanged(_map.Center);
                }
                Invalidate();

                if (mouseWheelRefreshTimer == null)
                {
                    mouseWheelRefreshTimer = new System.Timers.Timer(50);
                    mouseWheelRefreshTimer.Elapsed += new System.Timers.ElapsedEventHandler(timerUpdate);
                    mouseWheelRefreshTimer.Enabled = true;
                    mouseWheelRefreshTimer.AutoReset = false;
                    mouseWheelRefreshTimer.Start();
                }
                else
                {
                    mouseWheelRefreshTimer.Stop();
                    mouseWheelRefreshTimer.Start();
                }                
            }
        }

        void timerUpdate(object state, System.Timers.ElapsedEventArgs args)
        {
            logger.Debug("TimerRefresh");
            if (MapZoomChanged != null)
                MapZoomChanged(_map.Zoom);
            Refresh();
        }

        GeoPoint _dragStartCoord = null;
        double _orgScale = 0;
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (_map != null)
            {
                if (e.Button == MouseButtons.Left || e.Button == MouseButtons.Middle) //dragging
                {
                    _dragStartPoint = e.Location;
                    _dragEndPoint = e.Location;
                    _dragStartCoord = _map.Center;
                    _orgScale = _map.Zoom;
                }

                if (MouseDown != null)
                    MouseDown(_map.ImageToWorld(new Point(e.X, e.Y)), e);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (_map != null)
            {
                GeoPoint p = _map.ImageToWorld(new Point(e.X, e.Y));

                if (MouseMove != null)
                    MouseMove(p, e);

                bool isStartDrag = _image != null && e.Location != _dragStartPoint && !_dragging &&
                    (e.Button == MouseButtons.Left || e.Button == MouseButtons.Middle) &&   //Left of middle button can start drag
                    !(_setActiveToolNoneDuringRedraw && (_activeTool == Tools.DrawLine || _activeTool == Tools.DrawPoint || _activeTool == Tools.DrawPolygon)); //It should not be any of these tools

                if (isStartDrag)
                {
                    _dragging = true;
                }

                if (_dragging)
                {
                    if (MouseDrag != null)
                        MouseDrag(p, e);

                    //Pan can be if we have ActiveTool Pan and not doing a ShiftButtonZoom-Operation
                    bool isPanOperation = _activeTool == Tools.Pan && !(_shiftButtonDragRectangleZoom && (Control.ModifierKeys & Keys.Shift) != Keys.None);
                    
                    //Pan can also be if we are in a drawline/drawpoint/drawpoly operation and pressed left mousebutton while dragging..
                    //If we not are setting tool non while redrawing..
                    if ((_activeTool == Tools.DrawLine || _activeTool == Tools.DrawPolygon)
                        && e.Button == System.Windows.Forms.MouseButtons.Left && !_setActiveToolNoneDuringRedraw)
                    {
                        isPanOperation = true;    
                    }


                    //Zoom in or Zoom Out
                    bool isZoomOperation = _activeTool == Tools.ZoomIn || _activeTool == Tools.ZoomOut;

                    //Tool ZoomWindow or ShiftButtonDragRectangle
                    bool isZoomWindowOperation = _activeTool == Tools.ZoomWindow || _activeTool == Tools.Query || _activeTool == Tools.QueryGeometry || (_shiftButtonDragRectangleZoom && (Control.ModifierKeys & Keys.Shift) != Keys.None);

                    if (isPanOperation)
                    {
                        _dragEndPoint = ClipPoint(e.Location);
                        if (_dragStartCoord != null)
                        {
                            _map.Center = new GeoPoint(_dragStartCoord.X - _map.PixelSize * (_dragEndPoint.X - _dragStartPoint.X), _dragStartCoord.Y - _map.PixelSize * (_dragStartPoint.Y - _dragEndPoint.Y));
                            Invalidate(ClientRectangle);
                        }
                    }
                    else if (isZoomOperation)
                    {
                        _dragEndPoint = ClipPoint(e.Location);
                        if (_dragEndPoint.Y - _dragStartPoint.Y < 0) //Zoom out
                            _scaling = (float)Math.Pow(1 / (float)(_dragStartPoint.Y - _dragEndPoint.Y), 0.5);
                        else //Zoom in
                            _scaling = 1 + (_dragEndPoint.Y - _dragStartPoint.Y) * 0.1f;

                        _map.Zoom = _orgScale / _scaling;
                        if (MapZooming != null)
                            MapZooming(_map.Zoom);
                        
                        Invalidate(ClientRectangle);
                    }
                    else if (isZoomWindowOperation)
                    {
                        _dragEndPoint = ClipPoint(e.Location);
                        _rectangle = GenerateRectangle(_dragStartPoint, _dragEndPoint);
                        Invalidate(new Region(ClientRectangle));
                    }
                }
                else
                {
                    if (_activeTool == Tools.DrawPolygon || _activeTool == Tools.DrawLine)
                    {
                        _dragEndPoint = new Point(0, 0);
                        if (_pointArray != null)
                        {
                            _pointArray[_pointArray.GetUpperBound(0)] = Map.ImageToWorld(ClipPoint(e.Location));
                            _rectangle = GenerateRectangle(_dragStartPoint, ClipPoint(e.Location));
                            Invalidate(new Region(ClientRectangle));
                        }
                    }
                }
            }
        }


#if EnableMetafileClipboardSupport

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.C)
            {
                Clipboard.Clear();
                ClipboardMetafileHelper.PutEnhMetafileOnClipboard(Handle, _map.GetMapAsMetafile());
                e.Handled = true;
            }

            base.OnKeyDown(e);
        }
        
#endif
        protected virtual void OnMapChanging(CancelEventArgs e)
        {
            if (MapChanging != null) MapChanging(this, e);
            if (e.Cancel)
                return;
            
            if (_map != null)
            {
                VariableLayerCollection.VariableLayerCollectionRequery -= HandleVariableLayersRequery;
                _map.MapNewTileAvaliable -= HandleMapNewTileAvaliable;
            }
        }

        protected virtual void OnMapChanged(EventArgs e)
        {
            if (_map != null)
            {
                VariableLayerCollection.VariableLayerCollectionRequery += HandleVariableLayersRequery;
                _map.MapNewTileAvaliable += HandleMapNewTileAvaliable;
                Refresh();
            }
            
            if (MapChanged != null) MapChanged(this, e);
        }

        private void RegenerateZoomingImage()
        {
            Cursor c = Cursor;
            Cursor = Cursors.WaitCursor;
            _map.Zoom /= _scaling;
            lock (maplocker)
            {
                _image = _map.GetMap();
            }
            _scaling = 1;
            _dragImage = GenerateDragImage(PreviewModes.Best);
            _dragStartPoint = _dragEndPoint;
            Cursor = c;
        }

        private Bitmap GenerateDragImage(PreviewModes mode)
        {
            if (mode == PreviewModes.Best)
            {
                Cursor c = Cursor;
                Cursor = Cursors.WaitCursor;

                GeoPoint realCenter = _map.Center;
                Bitmap bmp = new Bitmap(_map.Size.Width * 3, _map.Size.Height * 3);
                Graphics g = Graphics.FromImage(bmp);

                for (int i = -1; i <= 1; i++)
                {
                    for (int j = -1; j <= 1; j++)
                    {
                        if (i == 0 && j == 0)
                        {
                            var clone = _image.Clone() as Image;
                            if (clone != null)
                                g.DrawImageUnscaled(clone, _map.Size.Width, _map.Size.Height);
                        }
                        else
                            g.DrawImageUnscaled(GeneratePartialBitmap(realCenter, (XPosition)i, (YPosition)j), (i + 1) * _map.Size.Width, (j + 1) * _map.Size.Height);
                    }
                }
                g.Dispose();
                _map.Center = realCenter;

                Cursor = c;

                return bmp;
            }
            if (_image.PixelFormat != PixelFormat.Undefined)
                return _image.Clone() as Bitmap;
            return null;
        }

        private Bitmap GeneratePartialBitmap(GeoPoint center, XPosition xPos, YPosition yPos)
        {
            double x = center.X, y = center.Y;

            switch (xPos)
            {
                case XPosition.Right:
                    x += _map.Envelope.Width;
                    break;
                case XPosition.Left:
                    x -= _map.Envelope.Width;
                    break;
            }

            switch (yPos)
            {
                case YPosition.Top:
                    y += _map.Envelope.Height;
                    break;
                case YPosition.Bottom:
                    y -= _map.Envelope.Height;
                    break;
            }

            _map.Center = new GeoPoint(x, y);
            return _map.GetMap() as Bitmap;
        }

        private Point ClipPoint(Point p)
        {
            int x = p.X < 0 ? 0 : (p.X > ClientSize.Width ? ClientSize.Width : p.X);
            int y = p.Y < 0 ? 0 : (p.Y > ClientSize.Height ? ClientSize.Height : p.Y);
            return new Point(x, y);
        }

        private static Rectangle GenerateRectangle(Point p1, Point p2)
        {
            int x = Math.Min(p1.X, p2.X);
            int y = Math.Min(p1.Y, p2.Y);
            int width = Math.Abs(p2.X - p1.X);
            int height = Math.Abs(p2.Y - p1.Y);

            return new Rectangle(x, y, width, height);
        }

        protected override void OnPaint(PaintEventArgs pe)
        {
            try
            {
                if (_dragging)
                {
                    if (_activeTool == Tools.ZoomWindow || _activeTool == Tools.Query || (_shiftButtonDragRectangleZoom && (Control.ModifierKeys & Keys.Shift) != Keys.None))
                    {
                        //Reset image to normal view
                        lock (lockerPaintImage)
                        {
                            Bitmap patch = (_image as Bitmap).Clone(pe.ClipRectangle, PixelFormat.DontCare);
                            pe.Graphics.DrawImageUnscaled(patch, pe.ClipRectangle);
                            patch.Dispose();
                        }

                        //Draw selection rectangle
                        if (_rectangle.Width > 0 && _rectangle.Height > 0)
                        {
                            pe.Graphics.FillRectangle(_rectangleBrush, _rectangle);
                            Rectangle border = new Rectangle(_rectangle.X + (int)_rectanglePen.Width / 2, _rectangle.Y + (int)_rectanglePen.Width / 2, _rectangle.Width - (int)_rectanglePen.Width, _rectangle.Height - (int)_rectanglePen.Width);
                            pe.Graphics.DrawRectangle(_rectanglePen, border);
                        }
                    }
                    else if (_activeTool == Tools.Pan || _activeTool == Tools.ZoomIn || _activeTool == Tools.ZoomOut || _activeTool == Tools.DrawLine || _activeTool == Tools.DrawPolygon)
                    {
                        if (_map.Envelope.Equals(_imageBoundingBox))
                        {
                            lock (lockerPaintImage)
                            {
                                pe.Graphics.DrawImageUnscaled(_image, 0, 0);
                            }
                        }
                        else
                        {
                            PointF ul = PointF.Empty;
                            PointF lr = PointF.Empty;

                            lock (_imageBoundingBox)
                            {
                                lock (lockerPaintImage)
                                {
                                    ul = _map.WorldToImage(_imageBoundingBox.TopLeft());
                                    lr = _map.WorldToImage(_imageBoundingBox.BottomRight());

                                    pe.Graphics.DrawImage(_image, RectangleF.FromLTRB(ul.X, ul.Y, lr.X, lr.Y));
                                }
                            }
                            if ((Math.Abs(ul.X) > 50 || Math.Abs(ul.Y) > 50) && !_isRefreshing)
                            {
                                //update the image we're dragging
                                int generation = ++_imageGeneration;
                                BoundingBox bbox = _map.Envelope;   
                            }
                        }
                    }
                    else if (_activeTool == Tools.ZoomIn || _activeTool == Tools.ZoomOut)
                    {
                        RectangleF rect = new RectangleF(0, 0, _map.Size.Width, _map.Size.Height);

                        if (_map.Zoom / _scaling < _map.MinimumZoom)
                            _scaling = (float)Math.Round(_map.Zoom / _map.MinimumZoom, 4);

                        if (_previewMode == PreviewModes.Best)
                            _scaling *= 3;

                        rect.Width *= _scaling;
                        rect.Height *= _scaling;

                        rect.Offset(_map.Size.Width / 2f - rect.Width / 2, _map.Size.Height / 2f - rect.Height / 2);


                        pe.Graphics.DrawImage(_dragImage, rect);
                    }
                }
                else if (_image != null && _image.PixelFormat != PixelFormat.Undefined)
                {
                    {
                        lock (lockerPaintImage)
                        {
                            if (_map.Envelope.Equals(_imageBoundingBox))
                            {
                                pe.Graphics.DrawImageUnscaled(_image, 0, 0);
                            }
                            else
                            {
                                var ul = _map.WorldToImage(_imageBoundingBox.TopLeft());
                                var lr = _map.WorldToImage(_imageBoundingBox.BottomRight());
                                pe.Graphics.DrawImage(_image, RectangleF.FromLTRB(ul.X, ul.Y, lr.X, lr.Y));
                            }
                        }
                    }
                }


                //Draws current line or polygon (Draw Line or Draw Polygon tool)
                if (_pointArray != null)
                {
                    if (_pointArray.GetUpperBound(0) == 1)
                    {
                        PointF p1 = Map.WorldToImage(_pointArray[0]);
                        PointF p2 = Map.WorldToImage(_pointArray[1]);
                        pe.Graphics.DrawLine(new Pen(Color.Gray, 2F), p1, p2);
                    }
                    else
                    {
                        PointF[] pts = new PointF[_pointArray.Length];
                        for (int i = 0; i < pts.Length; i++)
                            pts[i] = Map.WorldToImage(_pointArray[i]);

                        if (_activeTool == Tools.DrawPolygon)
                        {
                            Color c = Color.FromArgb(127, Color.Gray);
                            pe.Graphics.FillPolygon(new SolidBrush(c), pts);
                            pe.Graphics.DrawPolygon(new Pen(Color.Gray, 2F), pts);
                        }
                        else
                            pe.Graphics.DrawLines(new Pen(Color.Gray, 2F), pts);
                    }
                }


                base.OnPaint(pe);

                /*Draw Floating Map-Decorations*/
                if (_map != null && _map.Decorations != null)
                {
                    foreach (SharpMap.Rendering.Decoration.IMapDecoration md in _map.Decorations)
                    {
                        md.Render(pe.Graphics, _map);
                    }
                }
            }
            catch (Exception ee)
            {
                logger.Error(ee);
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (_map != null)
            {
                if (MouseUp != null)
                    MouseUp(_map.ImageToWorld(new Point(e.X, e.Y)), e);

                if (e.Button == MouseButtons.Left || e.Button == MouseButtons.Middle)
                {
                    if (_activeTool == Tools.ZoomOut)
                    {
                        double scale = 0.5;
                        if (_dragging)
                        {
                            if (e.Y - _dragStartPoint.Y < 0) //Zoom out
                                scale = (float)Math.Pow(1 / (float)(_dragStartPoint.Y - e.Y), 0.5);
                            else //Zoom in
                                scale = 1 + (e.Y - _dragStartPoint.Y) * 0.1;
                        }
                        else
                        {
                            _map.Center = _map.ImageToWorld(new Point(e.X, e.Y));

                            if (MapCenterChanged != null)
                                MapCenterChanged(_map.Center);
                        }

                        _map.Zoom /= scale;

                        if (MapZoomChanged != null)
                            MapZoomChanged(_map.Zoom);
                    }
                    else if (_activeTool == Tools.ZoomIn)
                    {
                        double scale = 2;
                        if (_dragging)
                        {
                            if (e.Y - _dragStartPoint.Y < 0) //Zoom out
                                scale = (float)Math.Pow(1 / (float)(_dragStartPoint.Y - e.Y), 0.5);
                            else //Zoom in
                                scale = 1 + (e.Y - _dragStartPoint.Y) * 0.1;
                        }
                        else
                        {
                            _map.Center = _map.ImageToWorld(new Point(e.X, e.Y));

                            if (MapCenterChanged != null)
                                MapCenterChanged(_map.Center);
                        }

                        _map.Zoom *= 1 / scale;

                        if (MapZoomChanged != null)
                            MapZoomChanged(_map.Zoom);

                    }
                    else if ((_activeTool == Tools.Pan && !(_shiftButtonDragRectangleZoom && (Control.ModifierKeys & Keys.Shift) != Keys.None)) ||
                        (e.Button == System.Windows.Forms.MouseButtons.Left && _dragging && (_activeTool == Tools.DrawLine || _activeTool == Tools.DrawPolygon)))
                    {
                        if (_dragging)
                        {
                            if (MapCenterChanged != null)
                                MapCenterChanged(_map.Center);
                        }
                        else
                        {
                            if (_panOnClick)
                            {
                                _map.Center = _map.ImageToWorld(new Point(e.X, e.Y));

                                if (MapCenterChanged != null)
                                    MapCenterChanged(_map.Center);
                            }
                        }
                    }
                    else if (_activeTool == Tools.Query || _activeTool == Tools.QueryGeometry)
                    {
                        if (_map.Layers.Count > _queryLayerIndex && _queryLayerIndex > -1)
                        {
                            var layer = _map.Layers[_queryLayerIndex] as ICanQueryLayer;
                            if (layer != null)
                            {
                                BoundingBox bounding;
                                bool isPoint = false;
                                if (_dragging)
                                {
                                    GeoPoint lowerLeft;
                                    GeoPoint upperRight;
                                    GetBounds(_map.ImageToWorld(_dragStartPoint), _map.ImageToWorld(_dragEndPoint), out lowerLeft, out upperRight);

                                    bounding = new BoundingBox(lowerLeft, upperRight);
                                }
                                else
                                {
                                    bounding = new Envelope(_map.ImageToWorld(new Point(e.X, e.Y)));
                                    bounding = bounding.Grow(_map.PixelSize * _queryGrowFactor);
                                    isPoint = true;
                                }

                                Data.FeatureDataSet ds = new Data.FeatureDataSet();
                                if (_activeTool == Tools.Query)
                                    layer.ExecuteIntersectionQuery(bounding, ds);
                                else
                                {
                                    IGeometry geom;
                                    if (isPoint && QueryGrowFactor == 0)
                                        geom = _map.Factory.CreatePoint(_map.ImageToWorld(new Point(e.X, e.Y)));
                                    else    
                                        geom = _map.Factory.ToGeometry(bounding);
                                    layer.ExecuteIntersectionQuery(geom, ds);
                                }

                                if (ds.Tables.Count > 0)
                                    if (MapQueried != null) MapQueried(ds.Tables[0]);
                                    else if (MapQueried != null) MapQueried(new Data.FeatureDataTable());
                            }

                        }
                        else
                            MessageBox.Show("No active layer to query");
                    }
                    else if (_activeTool == Tools.ZoomWindow || (_shiftButtonDragRectangleZoom && (Control.ModifierKeys & Keys.Shift) != Keys.None))
                    {
                        if (_rectangle.Width > 0 && _rectangle.Height > 0)
                        {
                            GeoPoint lowerLeft;
                            GeoPoint upperRight;
                            GetBounds(_map.ImageToWorld(_dragStartPoint), _map.ImageToWorld(_dragEndPoint), out lowerLeft, out upperRight);
                            _dragEndPoint.X = 0;
                            _dragEndPoint.Y = 0;

                            _map.ZoomToBox(new BoundingBox(lowerLeft, upperRight));
                            
                            if (MapZoomChanged != null)
                                MapZoomChanged(_map.Zoom);

                        }
                    }
                    else if (_activeTool == Tools.DrawPoint)
                    {
                        if (GeometryDefined != null)
                        {
                            GeometryDefined(_map.Factory.CreatePoint(Map.ImageToWorld(new PointF(e.X, e.Y))));
                        }
                    }
                    else if (_activeTool == Tools.DrawPolygon || _activeTool == Tools.DrawLine)
                    {
                        //pointArray = null;
                        if (_pointArray == null)
                        {
                            _pointArray = new GeoPoint[2];
                            _pointArray[0] = Map.ImageToWorld(e.Location);
                            _pointArray[1] = Map.ImageToWorld(e.Location);
                        }
                        else
                        {
                            var temp = new GeoPoint[_pointArray.GetUpperBound(0) + 2];
                            for (var i = 0; i <= _pointArray.GetUpperBound(0); i++)
                                temp[i] = _pointArray[i];

                            temp[temp.GetUpperBound(0)] = Map.ImageToWorld(e.Location);
                            _pointArray = temp;
                        }
                    }
                }


                if (_dragging)
                {
                    _dragging = false;
                    if (_activeTool == Tools.Query)
                        Invalidate(_rectangle);
                    if (_activeTool == Tools.ZoomWindow || _activeTool == Tools.Query)
                        _rectangle = Rectangle.Empty;

                    Refresh();
                }
                else if (_activeTool == Tools.ZoomIn || _activeTool == Tools.ZoomOut || _activeTool == Tools.Pan)
                {
                    Refresh();
                }
            }
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);

            if (_activeTool == Tools.DrawPolygon)
            {
                if (GeometryDefined != null)
                {
                    var cl = new NetTopologySuite.Geometries.CoordinateList(_pointArray);
                    cl.CloseRing();
                    GeometryDefined(Map.Factory.CreatePolygon(Map.Factory.CreateLinearRing(cl.ToCoordinateArray()), null));
                }
                ActiveTool = Tools.None;
            }

            else if (_activeTool == Tools.DrawLine)
            {
                if (GeometryDefined != null)
                {
                    GeometryDefined(Map.Factory.CreateLineString(_pointArray));
                }
                ActiveTool = Tools.None;
            }
        }


        private static void GetBounds(GeoPoint p1, GeoPoint p2,
            out GeoPoint lowerLeft, out GeoPoint upperRight)
        {
            lowerLeft = new GeoPoint(Math.Min(p1.X, p2.X), Math.Min(p1.Y, p2.Y));
            upperRight = new GeoPoint(Math.Max(p1.X, p2.X), Math.Max(p1.Y, p2.Y));

            logger.Debug("p1: " + p1);
            logger.Debug("p2: " + p2);
            logger.Debug("lowerLeft: " + lowerLeft);
            logger.Debug("upperRight: " + upperRight);
        }

         /// <summary>
    /// MouseWheelGrabber is a MessageFilter that enables mousewheelcapture on mapcontrol even if the control does 
    /// not have focus as long as the mouse is positioned over the control
    /// </summary>
    class MouseWheelGrabber : IMessageFilter
    {
        [DllImport("user32.dll")]
        public static extern IntPtr WindowFromPoint(Point lpPoint);
        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out Point lpPoint);

        public static IntPtr GetWindowUnderCursor()
        {
            Point ptCursor = new Point();

            if (!(GetCursorPos(out ptCursor)))
                return IntPtr.Zero;

            return WindowFromPoint(ptCursor);
        }

        private MapBox redirectHandle = null;
        public MouseWheelGrabber(MapBox redirectHandle)
        {
            this.redirectHandle = redirectHandle;
        }

        public bool PreFilterMessage(ref Message m)
        {
            if (m.Msg == 0x020A)
            {
                int delta = ((int)(m.WParam.ToInt64() & 0xFFFF0000) >> 16);
                Point pt = redirectHandle.PointToClient(new Point(m.LParam.ToInt32()));
                if (redirectHandle.ClientRectangle.Contains(pt))
                {
                    IntPtr hWnd = GetWindowUnderCursor();
                    if (hWnd == redirectHandle.Handle)
                    {
                        redirectHandle.OnMouseWheel(new MouseEventArgs(System.Windows.Forms.MouseButtons.Middle, 0, pt.X, pt.Y, delta));
                        return true;
                    }
                }

            }

            return false;
        }

    }
    }

   

#if EnableMetafileClipboardSupport
    public class ClipboardMetafileHelper
    {
        [DllImport("user32.dll")]
        static extern bool OpenClipboard(IntPtr hWndNewOwner);

        [DllImport("user32.dll")]
        static extern bool EmptyClipboard();

        [DllImport("user32.dll")]
        static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

        [DllImport("user32.dll")]
        static extern bool CloseClipboard();

        [DllImport("gdi32.dll")]
        static extern IntPtr CopyEnhMetaFile(IntPtr hemfSrc, IntPtr hNull);

        [DllImport("gdi32.dll")]
        static extern bool DeleteEnhMetaFile(IntPtr hemf);

        /// <summary>
        /// Puts the metafile to the clipboard
        /// </summary>
        /// <param name="hWnd">The handle</param>
        /// <param name="mf">The metafie</param>
        /// <remarks>Metafile mf is set to a state that is not valid inside this function.</remarks>
        /// <returns><see langword="true"/> if operation was successfull</returns>
        static public bool PutEnhMetafileOnClipboard(IntPtr hWnd, Metafile mf)
        {
            bool bResult = false;
            IntPtr hEmf = mf.GetHenhmetafile();

            if (!hEmf.Equals(new IntPtr(0)))
            {
                IntPtr hEmf2 = CopyEnhMetaFile(hEmf, new IntPtr(0));
                if (!hEmf2.Equals(new IntPtr(0)))
                {
                    if (OpenClipboard(hWnd))
                    {
                        if (EmptyClipboard())
                        {
                            IntPtr hRes = SetClipboardData(14 /*CF_ENHMETAFILE*/, hEmf2);
                            bResult = hRes.Equals(hEmf2);
                            CloseClipboard();
                        }
                    }
                }
                DeleteEnhMetaFile(hEmf);
            }
            return bResult;
        }
    }
#endif
}
#endif