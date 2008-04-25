﻿using System;
using System.IO;
using System.Web;
using SharpMap.Renderer;

namespace SharpMap.Presentation.AspNet
{
    public interface IWebMap
    {

        event EventHandler BeforeCreateMapRequestConfig;
        event EventHandler MapRequestConfigCreated;
        event EventHandler BeforeInitMap;
        event EventHandler MapInitDone;
        event EventHandler BeforeLoadLayers;
        event EventHandler<LayerLoadedEventArgs> LayerLoaded;
        event EventHandler LayersLoaded;
        event EventHandler BeforeLoadMapState;
        event EventHandler MapStateLoaded;
        event EventHandler BeforeMapRender;
        event EventHandler MapRenderDone;

        HttpContext Context { get; set; }

        //Func<TOutput, Stream> StreamBuilder { get; }


        /// <summary>
        /// Create the SharpMap object - particular implementations may do this afresh/deserialize an existing one/retrieve from session
        /// End result is this.Map should yield a non null value.
        /// </summary>
        void InitMap();

        /// <summary>
        /// Hook used to add layers to the map object. 
        /// Some possible implementations will not use it if the map was deserialized or retrieved from session.
        /// </summary>
        void LoadLayers();

        /// <summary>
        /// default implementation should call MapRequestConfiguration.ConfigureMap(this.Map);
        /// </summary>
        void ConfigureMap();

        /// <summary>
        /// Hook used to configure map state e.g feature selections particular to the context user.
        /// called after ConfigureMap
        /// </summary>
        void LoadMapState();

        /// <summary>
        /// Hook used to configure renderer e.g set compression settings, output format etc
        /// </summary>
        void ConfigureRenderer();

        /// <summary>
        /// The Map instance
        /// </summary>
        Map Map { get; set; }


        /// <summary>
        /// wrapper object which supplies IRenderer instances
        /// </summary>
        IMapRenderer MapRenderer { get; set; }

        IMapCacheProvider CacheProvider { get; set; }


        ///// <summary>
        ///// An instance of a factory which can create a configuration object from Request parameters
        ///// </summary>
        //IMapRequestConfigFactory<TMapRequestConfig> ConfigFactory { get; }


        /// <summary>
        /// The configuration object which determines the map setup
        /// </summary>
        IMapRequestConfig MapRequestConfig { get; }


        /// <summary>
        /// unwire any event handlers here before disposal
        /// </summary>
        void UnwireEvents();

        Stream Render(out string mimetype);


    }
}