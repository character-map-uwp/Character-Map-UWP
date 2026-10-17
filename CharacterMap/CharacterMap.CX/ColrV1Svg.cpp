#include "pch.h"
#include "ColrV1Svg.h"
#include "SVGGeometrySink.h"
#include "DWHelpers.h"

#include <dwrite_3.h>
#include <d2d1_3.h>
#include <wrl/client.h>
#include <string>
#include <vector>
#include <algorithm>
#include <cmath>

using namespace Microsoft::WRL;
using namespace CharacterMapCX;

namespace
{
	// Trim trailing zeros from a float, e.g. "1.500000" → "1.5"
	static std::string FmtF(float v)
	{
		char buf[64];
		snprintf(buf, sizeof(buf), "%.6f", v);
		std::string s(buf);
		auto dot = s.find('.');
		if (dot != std::string::npos)
		{
			s.erase(s.find_last_not_of('0') + 1);
			if (s.back() == '.') s.pop_back();
		}
		return s;
	}

	// Emit a CSS hex colour from a DWRITE_PAINT_COLOR
	static std::string ColorToHex(const DWRITE_PAINT_COLOR& c)
	{
		int r = static_cast<int>((std::min)(1.0f, (std::max)(0.0f, c.value.r)) * 255.0f + 0.5f);
		int g = static_cast<int>((std::min)(1.0f, (std::max)(0.0f, c.value.g)) * 255.0f + 0.5f);
		int b = static_cast<int>((std::min)(1.0f, (std::max)(0.0f, c.value.b)) * 255.0f + 0.5f);
		char buf[16];
		snprintf(buf, sizeof(buf), "#%02x%02x%02x", r, g, b);
		return buf;
	}

	// Return the D2D1_EXTEND_MODE → SVG spreadMethod string
	static const char* SpreadMethod(UINT32 extendMode)
	{
		switch (extendMode)
		{
		case 1:  return "reflect";
		case 2:  return "repeat";
		default: return "pad";
		}
	}

	// Map DWRITE_COLOR_COMPOSITE_MODE → CSS mix-blend-mode string (best-effort).
	static const char* CompositeMode(DWRITE_COLOR_COMPOSITE_MODE mode)
	{
		switch (mode)
		{
		case DWRITE_COLOR_COMPOSITE_SCREEN:          return "screen";
		case DWRITE_COLOR_COMPOSITE_OVERLAY:         return "overlay";
		case DWRITE_COLOR_COMPOSITE_DARKEN:          return "darken";
		case DWRITE_COLOR_COMPOSITE_LIGHTEN:         return "lighten";
		case DWRITE_COLOR_COMPOSITE_COLOR_DODGE:     return "color-dodge";
		case DWRITE_COLOR_COMPOSITE_COLOR_BURN:      return "color-burn";
		case DWRITE_COLOR_COMPOSITE_HARD_LIGHT:      return "hard-light";
		case DWRITE_COLOR_COMPOSITE_SOFT_LIGHT:      return "soft-light";
		case DWRITE_COLOR_COMPOSITE_DIFFERENCE:      return "difference";
		case DWRITE_COLOR_COMPOSITE_EXCLUSION:       return "exclusion";
		case DWRITE_COLOR_COMPOSITE_MULTIPLY:        return "multiply";
		case DWRITE_COLOR_COMPOSITE_HSL_HUE:         return "hue";
		case DWRITE_COLOR_COMPOSITE_HSL_SATURATION:  return "saturation";
		case DWRITE_COLOR_COMPOSITE_HSL_COLOR:       return "color";
		case DWRITE_COLOR_COMPOSITE_HSL_LUMINOSITY:  return "luminosity";
		case DWRITE_COLOR_COMPOSITE_PLUS:            return "plus-lighter";
		case DWRITE_COLOR_COMPOSITE_SRC_OVER:
		default:                                     return "normal";
		}
	}

	static DWRITE_MATRIX IdentityMatrix()
	{
		return { 1.0f, 0.0f, 0.0f, 1.0f, 0.0f, 0.0f };
	}

	static bool IsIdentity(const DWRITE_MATRIX& m)
	{
		return m.m11 == 1.0f && m.m12 == 0.0f &&
		       m.m21 == 0.0f && m.m22 == 1.0f &&
		       m.dx  == 0.0f && m.dy  == 0.0f;
	}

	// Matrix multiplication: [x, y, 1] * a * b
	static DWRITE_MATRIX MultiplyMatrix(const DWRITE_MATRIX& a, const DWRITE_MATRIX& b)
	{
		DWRITE_MATRIX r{};
		r.m11 = a.m11 * b.m11 + a.m12 * b.m21;
		r.m12 = a.m11 * b.m12 + a.m12 * b.m22;
		r.m21 = a.m21 * b.m11 + a.m22 * b.m21;
		r.m22 = a.m21 * b.m12 + a.m22 * b.m22;
		r.dx  = a.dx  * b.m11 + a.dy  * b.m21 + b.dx;
		r.dy  = a.dx  * b.m12 + a.dy  * b.m22 + b.dy;
		return r;
	}

	struct GlyphPathResult
	{
		std::string d;
		bool evenOdd = false;
	};

	struct PaintFill
	{
		std::string fill;
		float opacity = 1.0f;
		bool isPaint = false;
	};

	struct ColrSvgContext
	{
		IDWritePaintReader* reader;
		IDWriteFontFace3*   face;
		ID2D1Factory5*      d2dFactory;
		std::string         defaultColorHex;
		float               defaultAlpha;
		std::string         defs;
		std::string         body;
		int                 idCounter = 0;

		std::string NextId(const char* prefix)
		{
			return std::string(prefix) + std::to_string(idCounter++);
		}

		GlyphPathResult GetGlyphPath(UINT32 glyphIndex)
		{
			UINT16 idx = static_cast<UINT16>(glyphIndex);
			FLOAT advance = 0;
			ComPtr<ID2D1PathGeometry> geom;
			d2dFactory->CreatePathGeometry(&geom);
			ComPtr<ID2D1GeometrySink> sink;
			geom->Open(&sink);
			face->GetGlyphRunOutline(
				1.0f,
				&idx, &advance, nullptr, 1, FALSE, FALSE, sink.Get());
			sink->Close();

			ComPtr<SVGGeometrySink> svgSink = new (std::nothrow) SVGGeometrySink();
			geom->Stream(svgSink.Get());
			svgSink->Close();

			String^ pd = svgSink->GetPathData();
			if (pd == nullptr || pd->IsEmpty()) return {};

			std::wstring wpath(pd->Data());
			std::string path(wpath.begin(), wpath.end());
			bool evenOdd = false;
			if (path.size() > 3 && path[0] == 'F')
			{
				evenOdd = (path[1] == '0');
				path = path.substr(3);
			}
			return { path, evenOdd };
		}

		void AppendGradientStops(std::string& out, UINT32 stopCount)
		{
			std::vector<D2D1_GRADIENT_STOP> stops(stopCount);
			std::vector<DWRITE_PAINT_COLOR>  colors(stopCount);
			reader->GetGradientStops(0, stopCount, stops.data());
			reader->GetGradientStopColors(0, stopCount, colors.data());
			for (UINT32 i = 0; i < stopCount; ++i)
			{
				bool isText = (colors[i].colorAttributes & DWRITE_PAINT_ATTRIBUTES_USES_TEXT_COLOR) != 0;
				std::string hex = isText ? defaultColorHex : ColorToHex(colors[i]);
				float alpha = isText ? defaultAlpha : colors[i].value.a;
				out += "<stop offset=\"" + FmtF(stops[i].position) + "\""
					" stop-color=\"" + hex + "\"";
				if (alpha < 0.999f)
					out += " stop-opacity=\"" + FmtF(alpha) + "\"";
				out += "/>";
			}
		}

		std::string FormatMatrix(const DWRITE_MATRIX& m)
		{
			return "matrix(" + FmtF(m.m11) + " " + FmtF(m.m12) + " " +
			                   FmtF(m.m21) + " " + FmtF(m.m22) + " " +
			                   FmtF(m.dx)  + " " + FmtF(m.dy)  + ")";
		}

		bool TryResolvePaint(DWRITE_PAINT_ELEMENT& elem, const DWRITE_MATRIX& currentTransform, PaintFill& out)
		{
			switch (elem.paintType)
			{
			case DWRITE_PAINT_TYPE_SOLID:
			{
				bool isText = (elem.paint.solid.colorAttributes & DWRITE_PAINT_ATTRIBUTES_USES_TEXT_COLOR) != 0;
				out.fill = isText ? defaultColorHex : ColorToHex(elem.paint.solid);
				out.opacity = isText ? defaultAlpha : elem.paint.solid.value.a;
				out.isPaint = true;
				return true;
			}
			case DWRITE_PAINT_TYPE_LINEAR_GRADIENT:
			{
				const auto& lg = elem.paint.linearGradient;
				std::string id = NextId("lg");
				std::string gradDef = "<linearGradient id=\"" + id + "\""
					" gradientUnits=\"userSpaceOnUse\""
					" x1=\"" + FmtF(lg.x0) + "\" y1=\"" + FmtF(lg.y0) + "\""
					" x2=\"" + FmtF(lg.x1) + "\" y2=\"" + FmtF(lg.y1) + "\"";
				if (!IsIdentity(currentTransform))
					gradDef += " gradientTransform=\"" + FormatMatrix(currentTransform) + "\"";
				gradDef += " spreadMethod=\"" + std::string(SpreadMethod(lg.extendMode)) + "\">";
				AppendGradientStops(gradDef, lg.gradientStopCount);
				gradDef += "</linearGradient>";
				defs += gradDef;

				out.fill = "url(#" + id + ")";
				out.opacity = 1.0f;
				out.isPaint = true;
				return true;
			}
			case DWRITE_PAINT_TYPE_RADIAL_GRADIENT:
			{
				const auto& rg = elem.paint.radialGradient;
				std::string id = NextId("rg");
				std::string gradDef = "<radialGradient id=\"" + id + "\""
					" gradientUnits=\"userSpaceOnUse\""
					" cx=\"" + FmtF(rg.x1) + "\" cy=\"" + FmtF(rg.y1) + "\""
					" r=\"" + FmtF(rg.radius1) + "\""
					" fx=\"" + FmtF(rg.x0) + "\" fy=\"" + FmtF(rg.y0) + "\"";
				if (rg.radius0 > 0.0001f)
					gradDef += " fr=\"" + FmtF(rg.radius0) + "\"";
				if (!IsIdentity(currentTransform))
					gradDef += " gradientTransform=\"" + FormatMatrix(currentTransform) + "\"";
				gradDef += " spreadMethod=\"" + std::string(SpreadMethod(rg.extendMode)) + "\">";
				AppendGradientStops(gradDef, rg.gradientStopCount);
				gradDef += "</radialGradient>";
				defs += gradDef;

				out.fill = "url(#" + id + ")";
				out.opacity = 1.0f;
				out.isPaint = true;
				return true;
			}
			case DWRITE_PAINT_TYPE_SWEEP_GRADIENT:
			{
				const auto& sg = elem.paint.sweepGradient;
				std::string id = NextId("sg");
				std::string gradDef = "<radialGradient id=\"" + id + "\""
					" gradientUnits=\"userSpaceOnUse\""
					" cx=\"" + FmtF(sg.centerX) + "\" cy=\"" + FmtF(sg.centerY) + "\""
					" r=\"1\"";
				if (!IsIdentity(currentTransform))
					gradDef += " gradientTransform=\"" + FormatMatrix(currentTransform) + "\"";
				gradDef += " spreadMethod=\"" + std::string(SpreadMethod(sg.extendMode)) + "\">";
				AppendGradientStops(gradDef, sg.gradientStopCount);
				gradDef += "</radialGradient>";
				defs += gradDef;

				out.fill = "url(#" + id + ")";
				out.opacity = 1.0f;
				out.isPaint = true;
				return true;
			}
			case DWRITE_PAINT_TYPE_TRANSFORM:
			{
				DWRITE_MATRIX combined = MultiplyMatrix(elem.paint.transform, currentTransform);
				DWRITE_PAINT_ELEMENT child{};
				if (SUCCEEDED(reader->MoveToFirstChild(&child)))
				{
					bool isP = TryResolvePaint(child, combined, out);
					reader->MoveToParent();
					return isP;
				}
				return false;
			}
			default:
				return false;
			}
		}

		void Walk(DWRITE_PAINT_ELEMENT& elem, const std::string& clipPathId = "")
		{
			switch (elem.paintType)
			{
			case DWRITE_PAINT_TYPE_SOLID_GLYPH:
			{
				GlyphPathResult gp = GetGlyphPath(elem.paint.solidGlyph.glyphIndex);
				if (gp.d.empty()) return;

				bool isText = (elem.paint.solidGlyph.color.colorAttributes & DWRITE_PAINT_ATTRIBUTES_USES_TEXT_COLOR) != 0;
				std::string hex   = isText ? defaultColorHex : ColorToHex(elem.paint.solidGlyph.color);
				float        alpha = isText ? defaultAlpha     : elem.paint.solidGlyph.color.value.a;

				body += "<path d=\"" + gp.d + "\" fill=\"" + hex + "\"";
				if (alpha < 0.999f)
					body += " fill-opacity=\"" + FmtF(alpha) + "\"";
				if (gp.evenOdd)
					body += " fill-rule=\"evenodd\"";
				if (!clipPathId.empty())
					body += " clip-path=\"url(#" + clipPathId + ")\"";
				body += "/>";
				break;
			}

			case DWRITE_PAINT_TYPE_GLYPH:
			{
				UINT32 glyphIdx = elem.paint.glyph.glyphIndex;
				DWRITE_PAINT_ELEMENT child{};
				if (FAILED(reader->MoveToFirstChild(&child))) return;

				PaintFill paint{};
				if (TryResolvePaint(child, IdentityMatrix(), paint))
				{
					reader->MoveToParent();
					GlyphPathResult gp = GetGlyphPath(glyphIdx);
					if (!gp.d.empty())
					{
						body += "<path d=\"" + gp.d + "\" fill=\"" + paint.fill + "\"";
						if (paint.opacity < 0.999f)
							body += " fill-opacity=\"" + FmtF(paint.opacity) + "\"";
						if (gp.evenOdd)
							body += " fill-rule=\"evenodd\"";
						if (!clipPathId.empty())
							body += " clip-path=\"url(#" + clipPathId + ")\"";
						body += "/>";
					}
				}
				else
				{
					std::string cpId = NextId("cp");
					GlyphPathResult gp = GetGlyphPath(glyphIdx);
					defs += "<clipPath id=\"" + cpId + "\"><path d=\"" + gp.d + "\"";
					if (gp.evenOdd) defs += " fill-rule=\"evenodd\"";
					defs += "/></clipPath>";

					body += "<g clip-path=\"url(#" + cpId + ")\"";
					if (!clipPathId.empty())
						body += " clip-path=\"url(#" + clipPathId + ")\"";
					body += ">";

					Walk(child);
					reader->MoveToParent();

					body += "</g>";
				}
				break;
			}

			case DWRITE_PAINT_TYPE_TRANSFORM:
			{
				const DWRITE_MATRIX& m = elem.paint.transform;
				std::string tfStr = FormatMatrix(m);

				DWRITE_PAINT_ELEMENT child{};
				if (FAILED(reader->MoveToFirstChild(&child))) return;

				body += "<g transform=\"" + tfStr + "\"";
				if (!clipPathId.empty())
					body += " clip-path=\"url(#" + clipPathId + ")\"";
				body += ">";

				Walk(child);
				reader->MoveToParent();

				body += "</g>";
				break;
			}

			case DWRITE_PAINT_TYPE_LAYERS:
			{
				UINT32 count = elem.paint.layers.childCount;
				if (count == 0) return;
				DWRITE_PAINT_ELEMENT child{};
				if (FAILED(reader->MoveToFirstChild(&child))) return;

				bool hasGroup = !clipPathId.empty();
				if (hasGroup)
					body += "<g clip-path=\"url(#" + clipPathId + ")\">";

				Walk(child);

				for (UINT32 i = 1; i < count; ++i)
				{
					DWRITE_PAINT_ELEMENT sib{};
					if (FAILED(reader->MoveToNextSibling(&sib))) break;
					Walk(sib);
				}

				reader->MoveToParent();
				if (hasGroup)
					body += "</g>";
				break;
			}

			case DWRITE_PAINT_TYPE_COMPOSITE:
			{
				const char* blendMode = CompositeMode(elem.paint.composite.mode);

				DWRITE_PAINT_ELEMENT src{};
				if (FAILED(reader->MoveToFirstChild(&src))) return;

				std::string savedBody = body;
				body = "";
				Walk(src, clipPathId);
				std::string srcBody = body;

				DWRITE_PAINT_ELEMENT dst{};
				if (FAILED(reader->MoveToNextSibling(&dst)))
				{
					body = savedBody + srcBody;
					reader->MoveToParent();
					return;
				}
				body = "";
				Walk(dst, clipPathId);
				std::string dstBody = body;

				body = savedBody;
				reader->MoveToParent();

				body += "<g>" + dstBody + "<g style=\"mix-blend-mode:" + blendMode + "\">" + srcBody + "</g></g>";
				break;
			}

			case DWRITE_PAINT_TYPE_COLOR_GLYPH:
			{
				D2D_RECT_F clipBox = elem.paint.colorGlyph.clipBox;
				bool hasClip = (clipBox.left != 0 || clipBox.top != 0 ||
				                clipBox.right != 0 || clipBox.bottom != 0);

				std::string extraClip;
				if (hasClip)
				{
					extraClip = NextId("cgcp");
					defs += "<clipPath id=\"" + extraClip + "\">"
						"<rect x=\"" + FmtF(clipBox.left) + "\""
						" y=\"" + FmtF(clipBox.top) + "\""
						" width=\"" + FmtF(clipBox.right - clipBox.left) + "\""
						" height=\"" + FmtF(clipBox.bottom - clipBox.top) + "\"/>"
						"</clipPath>";
				}

				DWRITE_PAINT_ELEMENT child{};
				if (FAILED(reader->MoveToFirstChild(&child))) return;
				Walk(child, hasClip ? extraClip : clipPathId);
				reader->MoveToParent();
				break;
			}

			default:
				break;
			}
		}
	};
}

Platform::String^ ColrV1Svg::GetSvg(DWriteFontFace^ fontFace, UINT16 glyphIndex, Windows::UI::Color defaultColor)
{
	if (fontFace == nullptr)
		return nullptr;

	ComPtr<IDWriteFontFace3> rawFace = fontFace->GetFontFace();
	if (rawFace == nullptr)
		return nullptr;

	// Require IDWriteFontFace7 for the paint reader API.
	ComPtr<IDWriteFontFace7> face7;
	if (FAILED(rawFace.As(&face7)))
		return nullptr;

	DWRITE_PAINT_FEATURE_LEVEL featureLevel = face7->GetPaintFeatureLevel(DWRITE_GLYPH_IMAGE_FORMATS_COLR_PAINT_TREE);
	if (featureLevel < DWRITE_PAINT_FEATURE_LEVEL_COLR_V1)
		return nullptr;

	ComPtr<IDWritePaintReader> paintReader;
	if (FAILED(face7->CreatePaintReader(DWRITE_GLYPH_IMAGE_FORMATS_COLR_PAINT_TREE, featureLevel, &paintReader)))
		return nullptr;

	D2D1_COLOR_F dc = ToD2DColor(defaultColor);
	paintReader->SetTextColor(DWRITE_COLOR_F{ dc.r, dc.g, dc.b, dc.a });

	DWRITE_PAINT_ELEMENT rootElem{};
	D2D_RECT_F clipBox{};
	if (FAILED(paintReader->SetCurrentGlyph(glyphIndex, &rootElem, sizeof(rootElem), &clipBox)))
		return nullptr;

	if (rootElem.paintType == DWRITE_PAINT_TYPE_NONE)
		return nullptr;

	// Build a D2D factory for outline extraction.
	ComPtr<ID2D1Factory5> d2dFactory;
	{
		HRESULT hr = D2D1CreateFactory(D2D1_FACTORY_TYPE_SINGLE_THREADED,
			__uuidof(ID2D1Factory5), nullptr, reinterpret_cast<void**>(d2dFactory.GetAddressOf()));
		if (FAILED(hr)) return nullptr;
	}

	DWRITE_FONT_METRICS metrics{};
	rawFace->GetMetrics(&metrics);
	float upm = metrics.designUnitsPerEm > 0 ? static_cast<float>(metrics.designUnitsPerEm) : 2048.0f;

	char defaultHexBuf[16];
	snprintf(defaultHexBuf, sizeof(defaultHexBuf), "#%02x%02x%02x",
		(int)(dc.r * 255 + 0.5f), (int)(dc.g * 255 + 0.5f), (int)(dc.b * 255 + 0.5f));

	ColrSvgContext ctx{};
	ctx.reader         = paintReader.Get();
	ctx.face           = rawFace.Get();
	ctx.d2dFactory     = d2dFactory.Get();
	ctx.defaultColorHex = defaultHexBuf;
	ctx.defaultAlpha   = dc.a;

	ctx.Walk(rootElem);

	// Determine the clip box for the SVG viewBox.
	bool hasClipBox = (clipBox.left != 0 || clipBox.top != 0 || clipBox.right != 0 || clipBox.bottom != 0);
	float vbLeft, vbTop, vbW, vbH;
	if (hasClipBox)
	{
		vbLeft = clipBox.left;
		vbTop  = clipBox.top;
		vbW    = clipBox.right  - clipBox.left;
		vbH    = clipBox.bottom - clipBox.top;
	}
	else
	{
		float ascender  = static_cast<float>(metrics.ascent) / upm;
		float descender = static_cast<float>(metrics.descent) / upm;
		INT32 designAdv = 0;
		rawFace->GetDesignGlyphAdvances(1, &glyphIndex, &designAdv, FALSE);
		vbLeft = 0;
		vbTop  = -ascender;
		vbW    = (designAdv > 0 ? static_cast<float>(designAdv) : upm) / upm;
		vbH    = ascender + descender;
	}

	if (vbW <= 0.0f) vbW = 1.0f;
	if (vbH <= 0.0f) vbH = 1.0f;

	int pxW = 1024;
	int pxH = static_cast<int>(std::round(1024.0f * (vbH / vbW)));
	if (pxH <= 0) pxH = 1024;

	std::string svg =
		"<svg xmlns=\"http://www.w3.org/2000/svg\""
		" width=\"" + std::to_string(pxW) + "\" height=\"" + std::to_string(pxH) + "\""
		" viewBox=\"" +
		FmtF(vbLeft) + " " + FmtF(vbTop) + " " + FmtF(vbW) + " " + FmtF(vbH) +
		"\">"
		"<defs>" + ctx.defs + "</defs>"
		+ ctx.body +
		"</svg>";

	std::wstring wsvg(svg.begin(), svg.end());
	return ref new Platform::String(wsvg.c_str());
}
