#pragma once

#include "pch.h"
#include <Microsoft.Graphics.Canvas.native.h>
#include <d2d1_2.h>
#include <d2d1_3.h>
#include <dwrite_3.h>
#include "ColorTextAnalyzer.h"
#include "GlyphImageFormat.h"
#include <vector>
#include <string>
#include <iostream>

using namespace Microsoft::Graphics::Canvas;
using namespace Microsoft::Graphics::Canvas::Text;
using namespace Microsoft::WRL;
using namespace Windows::Foundation;
using namespace Windows::Foundation::Collections;
using namespace Platform;
using namespace std;

namespace CharacterMapCX
{
	class SVGGeometrySink : public ID2D1GeometrySink
	{
	public:

        virtual void SetOffset(float x, float y)
        {
            m_offsetX = x;
            m_offsetY = y;
        }

        virtual string ts(float value)
        {
            auto str = to_string(value);
            str.erase(str.find_last_not_of('0') + 1, string::npos); 
            str.erase(str.find_last_not_of('.') + 1, string::npos);
            return str;
        }

        virtual std::string ts(D2D1_POINT_2F p)
        {
            return ts(p.x + m_offsetX) + " " + ts(p.y + m_offsetY);
        }

        virtual void STDMETHODCALLTYPE SetFillMode(D2D1_FILL_MODE fillMode)
        {
            b = b + "F" + to_string(fillMode) + " ";
        }

        virtual void STDMETHODCALLTYPE BeginFigure(D2D1_POINT_2F startPoint, D2D1_FIGURE_BEGIN figureBegin)
        {
            b = b + "M " + ts(startPoint) + " ";
        }

        virtual void STDMETHODCALLTYPE AddLines(const D2D1_POINT_2F* points, UINT pointsCount)
        {
            m_hasData = true;
            for (int i = 0; i < pointsCount; i = i + 1)
            {
                b = b + "L " + ts(points[i]) + " ";
            }
        }

        virtual void STDMETHODCALLTYPE AddBeziers(const D2D1_BEZIER_SEGMENT* beziers, UINT beziersCount)
        {
            m_hasData = true;
            for (int i = 0; i < beziersCount; i = i + 1)
            {
                auto z = beziers[i];
                b = b + "C " + ts(z.point1) + " " + ts(z.point2) + " " + ts(z.point3) + " ";
            }
        }

        virtual void STDMETHODCALLTYPE EndFigure(D2D1_FIGURE_END figureEnd)
        {
            if (figureEnd == D2D1_FIGURE_END::D2D1_FIGURE_END_CLOSED)
                b = b + "Z ";
        }

        virtual void STDMETHODCALLTYPE SetSegmentFlags(D2D1_PATH_SEGMENT vertexFlags)
        {

        }

        virtual HRESULT STDMETHODCALLTYPE Close()
        {
            return S_OK;
        }

        IFACEMETHODIMP_(unsigned long) AddRef()
        {
            return InterlockedIncrement(&m_refCount);
        }

        IFACEMETHODIMP_(unsigned long) Release()
        {
            unsigned long newCount = InterlockedDecrement(&m_refCount);
            if (newCount == 0)
            {
                delete this;
                return 0;
            }

            return newCount;
        }

        IFACEMETHODIMP QueryInterface(
            IID const& riid,
            void** ppvObject
        )
        {
            if (__uuidof(IDWriteGeometrySink) == riid)
            {
                *ppvObject = this;
            }
            else if (__uuidof(IUnknown) == riid)
            {
                *ppvObject = this;
            }
            else
            {
                *ppvObject = nullptr;
                return E_FAIL;
            }

            this->AddRef();

            return S_OK;
        }

        String^ GetPathData()
        {
            if (m_hasData)
            {
                std::wstring wsTmp(b.begin(), b.end());
                auto ws = wsTmp;

                String^ s = ref new String(ws.c_str());
                return s;
            }
            else
                return ref new String();
        }

	private:

        string b = "";
        bool m_hasData = false;
        unsigned long m_refCount;

        float m_offsetX = 0;
        float m_offsetY = 0;

        // Inherited via ID2D1GeometrySink - Not required for text.
        void __stdcall AddLine(D2D1_POINT_2F point)
        {
        }

        void __stdcall AddBezier(const D2D1_BEZIER_SEGMENT* bezier)
        {
        }

        void __stdcall AddQuadraticBezier(const D2D1_QUADRATIC_BEZIER_SEGMENT* bezier)
        {
        }

        void __stdcall AddQuadraticBeziers(const D2D1_QUADRATIC_BEZIER_SEGMENT* beziers, UINT32 beziersCount)
        {
        }

        void __stdcall AddArc(const D2D1_ARC_SEGMENT* arc)
        {
        }
    };
}