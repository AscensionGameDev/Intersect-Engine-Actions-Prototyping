using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

using Intersect.Client.Classes.MonoGame.Graphics;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.GenericClasses;
using Intersect.Client.Framework.Graphics;
using Intersect.Client.General;
using Intersect.Client.Localization;
using Intersect.Utilities;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

using XNARectangle = Microsoft.Xna.Framework.Rectangle;
using XNAColor = Microsoft.Xna.Framework.Color;
using System.Globalization;
using System.Linq;

namespace Intersect.Client.MonoGame.Graphics
{

    public partial class MonoRenderer : GameRenderer
    {

        private readonly List<MonoTexture> mAllTextures = new List<MonoTexture>();

        private BasicEffect mBasicEffect;

        private ContentManager mContentManager;

        private GameBlendModes mCurrentBlendmode = GameBlendModes.None;

        private GameShader mCurrentShader;

        private FloatRect mCurrentSpriteView;

        private GameRenderTexture mCurrentTarget;

        private FloatRect mCurrentView;

        private BlendState mCutoutState;

        private int mDisplayHeight;

        private bool mDisplayModeChanged = false;

        private int mDisplayWidth;

        private int mFps;

        private int mFpsCount;

        private long mFpsTimer;

        private long mFsChangedTimer = -1;

        private Game mGame;

        private GameWindow mGameWindow;

        private GraphicsDeviceManager mGraphics;

        private GraphicsDevice mGraphicsDevice;

        private bool mInitialized;

        private bool mInitializing;

        private BlendState mMultiplyState;

        private BlendState mNormalState;

        private DisplayMode mOldDisplayMode;

        RasterizerState mRasterizerState = new RasterizerState() { ScissorTestEnable = true };

        private int mScreenHeight;

        private RenderTarget2D mScreenshotRenderTarget;

        private int mScreenWidth;

        private SpriteBatch mSpriteBatch;

        private bool mSpriteBatchBegan;

        private List<string> mValidVideoModes;

        private GameRenderTexture mWhiteTexture;

        public MonoRenderer(GraphicsDeviceManager graphics, ContentManager contentManager, Game monoGame)
        {
            mGame = monoGame;
            mGraphics = graphics;
            mContentManager = contentManager;

            mNormalState = new BlendState()
            {
                ColorSourceBlend = Blend.SourceAlpha,
                AlphaSourceBlend = Blend.One,
                ColorDestinationBlend = Blend.InverseSourceAlpha,
                AlphaDestinationBlend = Blend.One,
                ColorBlendFunction = BlendFunction.Add,
                AlphaBlendFunction = BlendFunction.Add,
            };

            mMultiplyState = new BlendState()
            {
                ColorBlendFunction = BlendFunction.Add,
                ColorSourceBlend = Blend.DestinationColor,
                ColorDestinationBlend = Blend.Zero
            };

            mCutoutState = new BlendState()
            {
                ColorBlendFunction = BlendFunction.Add,
                ColorSourceBlend = Blend.Zero,
                ColorDestinationBlend = Blend.InverseSourceAlpha,
                AlphaBlendFunction = BlendFunction.Add,
                AlphaSourceBlend = Blend.Zero,
                AlphaDestinationBlend = Blend.InverseSourceAlpha
            };

            mGameWindow = monoGame.Window;
        }

        public IList<string> ValidVideoModes => GetValidVideoModes();

        public void UpdateGraphicsState(int width, int height, bool initial = false)
        {
            var currentDisplayMode = mGraphics.GraphicsDevice.Adapter.CurrentDisplayMode;

            if (Globals.Database.FullScreen)
            {
                var supported = false;
                foreach (var mode in mGraphics.GraphicsDevice.Adapter.SupportedDisplayModes)
                {
                    if (mode.Width == width && mode.Height == height)
                    {
                        supported = true;
                    }
                }

                if (!supported)
                {
                    Globals.Database.FullScreen = false;
                    Globals.Database.SavePreferences();
                    Interface.Interface.MsgboxErrors.Add(
                        new KeyValuePair<string, string>(
                            Strings.Errors.displaynotsupported,
                            Strings.Errors.displaynotsupportederror.ToString(width + "x" + height)
                        )
                    );
                }
            }

            var fsChanged = mGraphics.IsFullScreen != Globals.Database.FullScreen && !Globals.Database.FullScreen;

            mGraphics.IsFullScreen = Globals.Database.FullScreen;
            if (fsChanged)
            {
                mGraphics.ApplyChanges();
            }

            mScreenWidth = width;
            mScreenHeight = height;
            mGraphics.PreferredBackBufferWidth = width;
            mGraphics.PreferredBackBufferHeight = height;
            mGraphics.SynchronizeWithVerticalRetrace = Globals.Database.TargetFps == 0;

            if (Globals.Database.TargetFps == 1)
            {
                mGame.TargetElapsedTime = new TimeSpan(333333);
            }
            else if (Globals.Database.TargetFps == 2)
            {
                mGame.TargetElapsedTime = new TimeSpan(333333 / 2);
            }
            else if (Globals.Database.TargetFps == 3)
            {
                mGame.TargetElapsedTime = new TimeSpan(333333 / 3);
            }
            else if (Globals.Database.TargetFps == 4)
            {
                mGame.TargetElapsedTime = new TimeSpan(333333 / 4);
            }

            mGame.IsFixedTimeStep = Globals.Database.TargetFps > 0;

            mGraphics.ApplyChanges();

            mDisplayWidth = mGraphics.GraphicsDevice.Adapter.CurrentDisplayMode.Width;
            mDisplayHeight = mGraphics.GraphicsDevice.Adapter.CurrentDisplayMode.Height;
            if (fsChanged || initial)
            {
                mGameWindow.Position = new Microsoft.Xna.Framework.Point(
                    (mDisplayWidth - mScreenWidth) / 2, (mDisplayHeight - mScreenHeight) / 2
                );
            }

            mOldDisplayMode = currentDisplayMode;
            if (fsChanged)
            {
                mFsChangedTimer = Timing.Global.Milliseconds + 1000;
            }

            if (fsChanged)
            {
                mDisplayModeChanged = true;
            }
        }

        public void CreateWhiteTexture()
        {
            mWhiteTexture = CreateRenderTexture(1, 1);
            mWhiteTexture.Begin();
            mWhiteTexture.Clear(Color.White);
            mWhiteTexture.End();
        }

        public override bool Begin()
        {
            //mGraphicsDevice.SetRenderTarget(null);
            if (mFsChangedTimer > -1 && mFsChangedTimer < Timing.Global.Milliseconds)
            {
                mGraphics.PreferredBackBufferWidth--;
                mGraphics.ApplyChanges();
                mGraphics.PreferredBackBufferWidth++;
                mGraphics.ApplyChanges();
                mFsChangedTimer = -1;
            }

            if (mGameWindow.ClientBounds.Width != 0 &&
                mGameWindow.ClientBounds.Height != 0 &&
                (mGameWindow.ClientBounds.Width != mScreenWidth || mGameWindow.ClientBounds.Height != mScreenHeight) &&
                !mGraphics.IsFullScreen)
            {
                if (mOldDisplayMode != mGraphics.GraphicsDevice.DisplayMode)
                {
                    mDisplayModeChanged = true;
                }

                UpdateGraphicsState(mScreenWidth, mScreenHeight);
            }

            StartSpritebatch(mCurrentView, GameBlendModes.None, null, null, true, null);

            return true;
        }

        public Pointf GetMouseOffset()
        {
            return new Pointf(
                mGraphics.PreferredBackBufferWidth / (float)mGameWindow.ClientBounds.Width,
                mGraphics.PreferredBackBufferHeight / (float)mGameWindow.ClientBounds.Height
            );
        }

        private void StartSpritebatch(
            FloatRect view,
            GameBlendModes mode = GameBlendModes.None,
            GameShader shader = null,
            GameRenderTexture target = null,
            bool forced = false,
            RasterizerState rs = null,
            bool drawImmediate = false
        )
        {
            var viewsDiff = view.X != mCurrentSpriteView.X ||
                            view.Y != mCurrentSpriteView.Y ||
                            view.Width != mCurrentSpriteView.Width ||
                            view.Height != mCurrentSpriteView.Height;

            if (mode != mCurrentBlendmode ||
                shader != mCurrentShader ||
                shader != null && shader.ValuesChanged() ||
                target != mCurrentTarget ||
                viewsDiff ||
                forced ||
                drawImmediate ||
                !mSpriteBatchBegan)
            {
                if (mSpriteBatchBegan)
                {
                    mSpriteBatch.End();
                }

                if (target != null)
                {
                    mGraphicsDevice?.SetRenderTarget((RenderTarget2D)target.GetTexture());
                }
                else
                {
                    mGraphicsDevice?.SetRenderTarget(mScreenshotRenderTarget);
                }

                var blend = mNormalState;
                Effect useEffect = null;

                switch (mode)
                {
                    case GameBlendModes.None:
                        blend = mNormalState;

                        break;

                    case GameBlendModes.Alpha:
                        blend = BlendState.AlphaBlend;

                        break;

                    case GameBlendModes.Multiply:
                        blend = mMultiplyState;

                        break;

                    case GameBlendModes.Add:
                        blend = BlendState.Additive;

                        break;

                    case GameBlendModes.Opaque:
                        blend = BlendState.Opaque;

                        break;

                    case GameBlendModes.Cutout:
                        blend = mCutoutState;

                        break;
                }

                if (shader != null)
                {
                    useEffect = (Effect)shader.GetShader();
                    shader.ResetChanged();
                }

                mSpriteBatch.Begin(
                    drawImmediate ? SpriteSortMode.Immediate : SpriteSortMode.Deferred, blend, SamplerState.PointClamp,
                    null, rs, useEffect,
                    Matrix.CreateRotationZ(0f) *
                    Matrix.CreateScale(new Vector3(1, 1, 1)) *
                    Matrix.CreateTranslation(-view.X, -view.Y, 0)
                );

                mCurrentSpriteView = view;
                mCurrentBlendmode = mode;
                mCurrentShader = shader;
                mCurrentTarget = target;
                mSpriteBatchBegan = true;
            }
        }

        public override bool DisplayModeChanged()
        {
            var changed = mDisplayModeChanged;
            mDisplayModeChanged = false;

            return changed;
        }

        public void EndSpriteBatch()
        {
            if (mSpriteBatchBegan)
            {
                mSpriteBatch.End();
            }

            mSpriteBatchBegan = false;
        }

        public static Microsoft.Xna.Framework.Color ConvertColor(Color clr)
        {
            return new Microsoft.Xna.Framework.Color(clr.R, clr.G, clr.B, clr.A);
        }

        public override void Clear(Color color)
        {
            mGraphicsDevice.Clear(ConvertColor(color));
        }

        public override void DrawTileBuffer(GameTileBuffer buffer)
        {
            EndSpriteBatch();
            mGraphicsDevice?.SetRenderTarget(mScreenshotRenderTarget);
            mGraphicsDevice.BlendState = mNormalState;
            mGraphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
            mGraphicsDevice.SamplerStates[0] = SamplerState.LinearClamp;
            mGraphicsDevice.DepthStencilState = DepthStencilState.None;

            ((MonoTileBuffer)buffer).Draw(mBasicEffect, mCurrentView);
        }

        public override void Close()
        {
        }

        public override GameTexture GetWhiteTexture()
        {
            return mWhiteTexture;
        }

        public ContentManager GetContentManager()
        {
            return mContentManager;
        }

        public override GameRenderTexture CreateRenderTexture(int width, int height)
        {
            return new MonoRenderTexture(mGraphicsDevice, width, height);
        }

        public override void DrawString(
            string text,
            GameFont gameFont,
            float x,
            float y,
            float fontScale,
            Color fontColor,
            bool worldPos = true,
            GameRenderTexture renderTexture = null,
            Color borderColor = null
        )
        {
            if (gameFont == null)
            {
                return;
            }

            var font = (SpriteFont)gameFont.GetFont();
            if (font == null)
            {
                return;
            }

            StartSpritebatch(mCurrentView, GameBlendModes.None, null, renderTexture, false, null);
            foreach (var chr in text)
            {
                if (!font.Characters.Contains(chr))
                {
                    text = text.Replace(chr, ' ');
                }
            }

            if (borderColor != null && borderColor != Color.Transparent)
            {
                mSpriteBatch.DrawString(
                    font, text, new Vector2(x, y - 1), ConvertColor(borderColor), 0f, Vector2.Zero,
                    new Vector2(fontScale, fontScale), SpriteEffects.None, 0
                );

                mSpriteBatch.DrawString(
                    font, text, new Vector2(x - 1, y), ConvertColor(borderColor), 0f, Vector2.Zero,
                    new Vector2(fontScale, fontScale), SpriteEffects.None, 0
                );

                mSpriteBatch.DrawString(
                    font, text, new Vector2(x + 1, y), ConvertColor(borderColor), 0f, Vector2.Zero,
                    new Vector2(fontScale, fontScale), SpriteEffects.None, 0
                );

                mSpriteBatch.DrawString(
                    font, text, new Vector2(x, y + 1), ConvertColor(borderColor), 0f, Vector2.Zero,
                    new Vector2(fontScale, fontScale), SpriteEffects.None, 0
                );
            }

            mSpriteBatch.DrawString(font, text, new Vector2(x, y), ConvertColor(fontColor));
        }

        public override void DrawString(
            string text,
            GameFont gameFont,
            float x,
            float y,
            float fontScale,
            Color fontColor,
            bool worldPos,
            GameRenderTexture renderTexture,
            FloatRect clipRect,
            Color borderColor = null
        )
        {
            if (gameFont == null)
            {
                return;
            }

            x += mCurrentView.X;
            y += mCurrentView.Y;

            //clipRect.X += _currentView.X;
            //clipRect.Y += _currentView.Y;
            var font = (SpriteFont)gameFont.GetFont();
            if (font == null)
            {
                return;
            }

            var clr = ConvertColor(fontColor);

            //Copy the current scissor rect so we can restore it after
            var currentRect = mSpriteBatch.GraphicsDevice.ScissorRectangle;
            StartSpritebatch(mCurrentView, GameBlendModes.None, null, renderTexture, false, mRasterizerState, true);

            //Set the current scissor rectangle
            mSpriteBatch.GraphicsDevice.ScissorRectangle = new Microsoft.Xna.Framework.Rectangle(
                (int)clipRect.X, (int)clipRect.Y, (int)clipRect.Width, (int)clipRect.Height
            );

            foreach (var chr in text)
            {
                if (!font.Characters.Contains(chr))
                {
                    text = text.Replace(chr, ' ');
                }
            }

            if (borderColor != null && borderColor != Color.Transparent)
            {
                mSpriteBatch.DrawString(
                    font, text, new Vector2(x, y - 1), ConvertColor(borderColor), 0f, Vector2.Zero,
                    new Vector2(fontScale, fontScale), SpriteEffects.None, 0
                );

                mSpriteBatch.DrawString(
                    font, text, new Vector2(x - 1, y), ConvertColor(borderColor), 0f, Vector2.Zero,
                    new Vector2(fontScale, fontScale), SpriteEffects.None, 0
                );

                mSpriteBatch.DrawString(
                    font, text, new Vector2(x + 1, y), ConvertColor(borderColor), 0f, Vector2.Zero,
                    new Vector2(fontScale, fontScale), SpriteEffects.None, 0
                );

                mSpriteBatch.DrawString(
                    font, text, new Vector2(x, y + 1), ConvertColor(borderColor), 0f, Vector2.Zero,
                    new Vector2(fontScale, fontScale), SpriteEffects.None, 0
                );
            }

            mSpriteBatch.DrawString(
                font, text, new Vector2(x, y), clr, 0f, Vector2.Zero, new Vector2(fontScale, fontScale),
                SpriteEffects.None, 0
            );

            EndSpriteBatch();

            //Reset scissor rectangle to the saved value
            mSpriteBatch.GraphicsDevice.ScissorRectangle = currentRect;
        }

        public override GameTileBuffer CreateTileBuffer()
        {
            return new MonoTileBuffer(mGraphicsDevice);
        }

        public override void DrawTexture(
            GameTexture tex,
            float sx,
            float sy,
            float sw,
            float sh,
            float tx,
            float ty,
            float tw,
            float th,
            Color renderColor,
            GameRenderTexture renderTarget = null,
            GameBlendModes blendMode = GameBlendModes.None,
            GameShader shader = null,
            float rotationDegrees = 0,
            bool isUi = false,
            bool drawImmediate = false
        )
        {
            var texture = tex?.GetTexture();
            if (texture == null)
            {
                return;
            }

            var packRotated = false;

            var pack = tex.GetTexturePackFrame();
            if (pack != null)
            {
                if (pack.Rotated)
                {
                    rotationDegrees -= 90;
                    var z = tw;
                    tw = th;
                    th = z;

                    z = sx;
                    sx = pack.Rect.Right - sy - sh;
                    sy = pack.Rect.Top + z;

                    z = sw;
                    sw = sh;
                    sh = z;
                    packRotated = true;
                }
                else
                {
                    sx += pack.Rect.X;
                    sy += pack.Rect.Y;
                }
            }

            var origin = Vector2.Zero;
            if (Math.Abs(rotationDegrees) > 0.01)
            {
                rotationDegrees = (float)(Math.PI / 180 * rotationDegrees);
                origin = new Vector2(sw / 2f, sh / 2f);

                float pntX = 0,
                    pntY = 0,
                    pnt1X = tw,
                    pnt1Y = 0,
                    pnt2X = 0,
                    pnt2Y = th,
                    cntrX = tw / 2,
                    cntrY = th / 2;
                Rotate(ref pntX, ref pntY, cntrX, cntrY, rotationDegrees);
                Rotate(ref pnt1X, ref pnt1Y, cntrX, cntrY, rotationDegrees);
                Rotate(ref pnt2X, ref pnt2Y, cntrX, cntrY, rotationDegrees);

                var width = (int)Math.Round(GetDistance(pntX, pntY, pnt1X, pnt1Y));
                var height = (int)Math.Round(GetDistance(pntX, pntY, pnt2X, pnt2Y));

                if (packRotated)
                {
                    (width, height) = (height, width);
                }

                tx += width / 2f;
                ty += height / 2f;
            }

            // Cache the result of ConvertColor(renderColor) in a temporary variable.
            var color = ConvertColor(renderColor);

            // Use a single Draw method to avoid duplicating code.
            void Draw(FloatRect currentView, GameRenderTexture targetObject)
            {
                StartSpritebatch(currentView, blendMode, shader, targetObject, false, null, drawImmediate);
                mSpriteBatch.Draw((Texture2D)texture, new Vector2(tx, ty),
                    new XNARectangle((int)sx, (int)sy, (int)sw, (int)sh), color, rotationDegrees, origin,
                    new Vector2(tw / sw, th / sh), SpriteEffects.None, 0);
            }

            if (renderTarget == null)
            {
                if (isUi)
                {
                    tx += mCurrentView.X;
                    ty += mCurrentView.Y;
                }

                Draw(mCurrentView, null);
            }
            else
            {
                Draw(new FloatRect(0, 0, renderTarget.GetWidth(), renderTarget.GetHeight()), renderTarget);
            }
        }

        private static double GetDistance(double x1, double y1, double x2, double y2)
        {
            var a2 = Math.Pow(x2 - x1, 2);
            var b2 = Math.Pow(y2 - y1, 2);
            var root = Math.Sqrt(a2 + b2);

            return root;
        }

        private void Rotate(ref float x, ref float y, float centerX, float centerY, float angle)
        {
            // Rotate the point around the center point.
            float s = (float)Math.Sin(angle);
            float c = (float)Math.Cos(angle);

            x -= centerX;
            y -= centerY;

            float newX = x * c - y * s;
            float newY = x * s + y * c;

            x = newX + centerX;
            y = newY + centerY;
        }

        public override void End()
        {
            EndSpriteBatch();
            mFpsCount++;
            if (mFpsTimer < Timing.Global.Milliseconds)
            {
                mFps = mFpsCount;
                mFpsCount = 0;
                mFpsTimer = Timing.Global.Milliseconds + 1000;
                mGameWindow.Title = Strings.Main.gamename;
            }

            foreach (var texture in mAllTextures)
            {
                texture?.Update();
            }
        }

        public override int GetFps()
        {
            return mFps;
        }

        public override int GetScreenHeight()
        {
            return mScreenHeight;
        }

        public override int GetScreenWidth()
        {
            return mScreenWidth;
        }

        public override string GetResolutionString()
        {
            return mScreenWidth + "x" + mScreenHeight;
        }

        public override List<string> GetValidVideoModes()
        {
            if (mValidVideoModes != null)
            {
                return mValidVideoModes;
            }

            mValidVideoModes = new List<string>();

            var allowedResolutions = new[]
            {
                new Resolution(800, 600),
                new Resolution(1024, 768),
                new Resolution(1024, 720),
                new Resolution(1280, 720),
                new Resolution(1280, 768),
                new Resolution(1280, 1024),
                new Resolution(1360, 768),
                new Resolution(1366, 768),
                new Resolution(1440, 1050),
                new Resolution(1440, 900),
                new Resolution(1600, 900),
                new Resolution(1680, 1050),
                new Resolution(1920, 1080)
            };

            var displayWidth = mGraphicsDevice?.DisplayMode?.Width;
            var displayHeight = mGraphicsDevice?.DisplayMode?.Height;

            foreach (var resolution in allowedResolutions)
            {
                if (resolution.X > displayWidth)
                {
                    continue;
                }

                if (resolution.Y > displayHeight)
                {
                    continue;
                }

                mValidVideoModes.Add(resolution.ToString());
            }

            return mValidVideoModes;
        }

        public override FloatRect GetView()
        {
            return mCurrentView;
        }

        public override void Init()
        {
            if (mInitializing)
            {
                return;
            }

            mInitializing = true;

            var database = Globals.Database;
            var validVideoModes = GetValidVideoModes();
            var targetResolution = Intersect.Utilities.MathHelper.Clamp(database.TargetResolution, 0, validVideoModes?.Count ?? 0);

            if (targetResolution != database.TargetResolution)
            {
                Debug.Assert(database != null, "database != null");
                database.TargetResolution = 0;
                database.SavePreference("Resolution", database.TargetResolution.ToString(CultureInfo.InvariantCulture));
            }

            var targetVideoMode = validVideoModes?[targetResolution];
            if (Resolution.TryParse(targetVideoMode, out var resolution))
            {
                PreferredResolution = resolution;
            }

            mGraphics.PreferredBackBufferWidth = PreferredResolution.X;
            mGraphics.PreferredBackBufferHeight = PreferredResolution.Y;

            UpdateGraphicsState(ActiveResolution.X, ActiveResolution.Y, true);

            if (mWhiteTexture == null)
            {
                CreateWhiteTexture();
            }

            mInitializing = false;
        }

        public void Init(GraphicsDevice graphicsDevice)
        {
            mGraphicsDevice = graphicsDevice;
            mBasicEffect = new BasicEffect(mGraphicsDevice);
            mBasicEffect.LightingEnabled = false;
            mBasicEffect.TextureEnabled = true;
            mSpriteBatch = new SpriteBatch(mGraphicsDevice);
        }

        public override GameFont LoadFont(string filename)
        {
            // Get font size from filename, format should be name_size.xnb or whatever
            var name = GameContentManager.RemoveExtension(filename)
                .Replace(Path.Combine("resources", "fonts"), "")
                .TrimStart(Path.DirectorySeparatorChar);

            // Split the name into parts
            var parts = name.Split('_');

            // Check if the font size can be extracted
            if (parts.Length < 1 || !int.TryParse(parts[parts.Length - 1], out var size))
            {
                return null;
            }

            // Concatenate the parts of the name except the last one to get the full name
            name = string.Join("_", parts.Take(parts.Length - 1));

            // Return a new MonoFont with the extracted name and size
            return new MonoFont(name, filename, size, mContentManager);
        }

        public override GameShader LoadShader(string shaderName)
        {
            return new MonoShader(shaderName, mContentManager);
        }

        public override GameTexture LoadTexture(string filename, string realFilename)
        {
            var packFrame = GameTexturePacks.GetFrame(filename);
            if (packFrame != null)
            {
                var tx = new MonoTexture(mGraphicsDevice, filename, packFrame);
                mAllTextures.Add(tx);

                return tx;
            }

            var tex = new MonoTexture(mGraphicsDevice, filename, realFilename);
            mAllTextures.Add(tex);

            return tex;
        }

        /// <inheritdoc />
        public override GameTexture LoadTexture(string assetName, Func<Stream> createStream) =>
            new MonoTexture(mGraphicsDevice, assetName, createStream);

        public override Pointf MeasureText(string text, GameFont gameFont, float fontScale)
        {
            var font = (SpriteFont)gameFont?.GetFont();
            if (font == null)
            {
                return Pointf.Empty;
            }

            foreach (var chr in text)
            {
                if (!font.Characters.Contains(chr))
                {
                    text = text.Replace(chr, ' ');
                }
            }

            var size = font.MeasureString(text);

            return new Pointf(size.X * fontScale, size.Y * fontScale);
        }

        public override void SetView(FloatRect view)
        {
            mCurrentView = view;

            Matrix.CreateOrthographicOffCenter(0, view.Width, view.Height, 0, 0f, -1, out var projection);
            projection.M41 += -0.5f * projection.M11;
            projection.M42 += -0.5f * projection.M22;
            mBasicEffect.Projection = projection;
            mBasicEffect.View = Matrix.CreateRotationZ(0f) *
                                Matrix.CreateScale(new Vector3(1f, 1f, 1f)) *
                                Matrix.CreateTranslation(-view.X, -view.Y, 0);
        }

        public override bool BeginScreenshot()
        {
            if (mGraphicsDevice == null)
            {
                return false;
            }

            mScreenshotRenderTarget = new RenderTarget2D(
                mGraphicsDevice, mScreenWidth, mScreenHeight, false,
                mGraphicsDevice.PresentationParameters.BackBufferFormat,
                mGraphicsDevice.PresentationParameters.DepthStencilFormat,
                mGraphicsDevice.PresentationParameters.MultiSampleCount, RenderTargetUsage.PreserveContents
            );

            return true;
        }

        public override void EndScreenshot()
        {
            if (mScreenshotRenderTarget == null)
            {
                return;
            }

            ScreenshotRequests.ForEach(
                screenshotRequestStream =>
                {
                    if (screenshotRequestStream == null)
                    {
                        return;
                    }

                    mScreenshotRenderTarget.SaveAsPng(
                        screenshotRequestStream, mScreenshotRenderTarget.Width, mScreenshotRenderTarget.Height
                    );

                    screenshotRequestStream.Close();
                }
            );

            ScreenshotRequests.Clear();

            if (mGraphicsDevice == null)
            {
                return;
            }

            var skippedFrame = mScreenshotRenderTarget;
            mScreenshotRenderTarget = null;
            mGraphicsDevice.SetRenderTarget(null);

            if (!Begin())
            {
                return;
            }

            mSpriteBatch?.Draw(skippedFrame, new XNARectangle(), XNAColor.White);
            End();
        }

    }

}
