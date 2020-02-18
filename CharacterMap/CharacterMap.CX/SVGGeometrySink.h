//#pragma once
//
//#include "pch.h"
//#include <Microsoft.Graphics.Canvas.native.h>
//#include <d2d1_2.h>
//#include <d2d1_3.h>
//#include <dwrite_3.h>
//#include "ColorTextAnalyzer.h"
//#include "GlyphImageFormat.h"
//#include <vector>
//#include <string>
//#include <iostream>
//
//using namespace Microsoft::Graphics::Canvas;
//using namespace Microsoft::Graphics::Canvas::Text;
//using namespace Microsoft::WRL;
//using namespace Windows::Foundation;
//using namespace Windows::Foundation::Collections;
//using namespace Platform;
//using namespace std;
//
//namespace CharacterMapCX
//{
//	class SVGGeometrySink : public IDWriteGeometrySink
//	{
//	public:
//
//        virtual void STDMETHODCALLTYPE SetFillMode(D2D1_FILL_MODE fillMode)
//        {
//            if (b.length == 0)
//                return;
//
//            auto c = to_wstring(fillMode).c_str();
//
//            b.append(wstr("F"));
//            b.append(c);
//            b.append(wstr(" "));
//        }
//
//        virtual void STDMETHODCALLTYPE SetSegmentFlags(D2D1_PATH_SEGMENT vertexFlags)
//        {
//
//        }
//
//        virtual void STDMETHODCALLTYPE BeginFigure(D2D1_POINT_2F startPoint, D2D1_FIGURE_BEGIN figureBegin)
//        {
//            b.append(wstr("M "));
//            b.append(to_wstring(startPoint.x));
//            b.append(wstr(" "));
//            b.append(to_wstring(startPoint.y));
//            b.append(wstr(" "));
//        }
//
//        virtual void STDMETHODCALLTYPE AddLines(const D2D1_POINT_2F* points, UINT pointsCount)
//        {
//            for (int i = 0; i < pointsCount; i = i + 1)
//            {
//                auto point = points[i];
//                b.append(wstr("L "));
//                b.append(to_wstring(point.x));
//                b.append(wstr(" "));
//                b.append(to_wstring(point.y));
//                b.append(wstr(" "));
//            }
//        }
//
//        virtual void STDMETHODCALLTYPE AddBeziers(const D2D1_BEZIER_SEGMENT* beziers, UINT beziersCount)
//        {
//            for (int i = 0; i < beziersCount; i = i + 1)
//            {
//                auto z = beziers[i];
//                b.append(wstr("C "));
//                b.append(to_wstring(z.point1.x));
//                b.append(wstr(" "));
//                b.append(to_wstring(z.point1.y));
//                b.append(wstr(" "));
//                b.append(to_wstring(z.point2.x));
//                b.append(wstr(" "));
//                b.append(to_wstring(z.point2.y));
//                b.append(wstr(" "));
//                b.append(to_wstring(z.point3.x));
//                b.append(wstr(" "));
//                b.append(to_wstring(z.point3.y));
//                b.append(wstr(" "));
//            }
//        }
//
//        virtual void STDMETHODCALLTYPE EndFigure(D2D1_FIGURE_END figureEnd)
//        {
//            b.append(wstr("Z "));
//            b.append(wstr(" "));
//        }
//
//        virtual HRESULT STDMETHODCALLTYPE Close()
//        {
//        }
//
//        IFACEMETHODIMP_(unsigned long) AddRef()
//        {
//            return InterlockedIncrement(&m_refCount);
//        }
//
//        IFACEMETHODIMP_(unsigned long) Release()
//        {
//            unsigned long newCount = InterlockedDecrement(&m_refCount);
//            if (newCount == 0)
//            {
//                delete this;
//                return 0;
//            }
//
//            return newCount;
//        }
//
//        IFACEMETHODIMP QueryInterface(
//            IID const& riid,
//            void** ppvObject
//        )
//        {
//            if (__uuidof(IDWriteGeometrySink) == riid)
//            {
//                *ppvObject = this;
//            }
//            else if (__uuidof(IUnknown) == riid)
//            {
//                *ppvObject = this;
//            }
//            else
//            {
//                *ppvObject = nullptr;
//                return E_FAIL;
//            }
//
//            this->AddRef();
//
//            return S_OK;
//        }
//
//        String^ GetPathData()
//        {
//            String^ s = ref new String(b.c_str());
//            return s;
//        }
//
//	private:
//        wstring b = wstr("");
//        unsigned long m_refCount;
//
//	};
//}