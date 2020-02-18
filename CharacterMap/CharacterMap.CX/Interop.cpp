#include "pch.h"
#include "Interop.h"
#include "CanvasTextLayoutAnalysis.h"

using namespace Microsoft::WRL;
using namespace CharacterMapCX;

Interop::Interop(CanvasDevice^ device)
{
	DWriteCreateFactory(DWRITE_FACTORY_TYPE::DWRITE_FACTORY_TYPE_SHARED, __uuidof(IDWriteFactory4), &m_dwriteFactory);

	// Initialize Direct2D resources.
	D2D1_FACTORY_OPTIONS options;
	ZeroMemory(&options, sizeof(D2D1_FACTORY_OPTIONS));

	D2D1CreateFactory(
		D2D1_FACTORY_TYPE_SINGLE_THREADED,
		__uuidof(ID2D1Factory5),
		&options,
		&m_d2dFactory
	);

	ComPtr<ID2D1Device1> d2ddevice = GetWrappedResource<ID2D1Device1>(device);
	d2ddevice->CreateDeviceContext(
		D2D1_DEVICE_CONTEXT_OPTIONS_NONE,
		&m_d2dContext);
}

CanvasFontSet^ Interop::GetSystemFonts(bool includeDownloadableFonts)
{
	ComPtr<IDWriteFontCollection1> fontCollection;
	ComPtr<IDWriteFontSet> fontSet;
	ThrowIfFailed(m_dwriteFactory->GetSystemFontCollection(includeDownloadableFonts, &fontCollection, true));
	fontCollection->GetFontSet(&fontSet);
	fontCollection = nullptr;
	return GetOrCreate<CanvasFontSet>(fontSet.Get());
}

CanvasTextLayoutAnalysis^ Interop::AnalyzeFontLayout(CanvasTextLayout^ layout)
{
	ComPtr<IDWriteTextLayout3> context = GetWrappedResource<IDWriteTextLayout4>(layout);

	ComPtr<ColorTextAnalyzer> ana = new (std::nothrow) ColorTextAnalyzer(m_d2dFactory, m_dwriteFactory, m_d2dContext);
	context->Draw(m_d2dContext.Get(), ana.Get(), 0, 0);
	CanvasTextLayoutAnalysis^ analysis = ref new CanvasTextLayoutAnalysis(ana);

	ana = nullptr;
	return analysis;
}

CanvasTextLayoutAnalysis^ Interop::AnalyzeCharacterLayout(CanvasTextLayout^ layout)
{
	ComPtr<IDWriteTextLayout3> context = GetWrappedResource<IDWriteTextLayout4>(layout);

	ComPtr<ColorTextAnalyzer> ana = new (std::nothrow) ColorTextAnalyzer(m_d2dFactory, m_dwriteFactory, m_d2dContext);
	ana->IsCharacterAnalysisMode = true;
	context->Draw(m_d2dContext.Get(), ana.Get(), 0, 0);

	CanvasTextLayoutAnalysis^ analysis = ref new CanvasTextLayoutAnalysis(ana);

	ana = nullptr;
	return analysis;
}

bool Interop::HasValidFonts(Uri^ uri)
{
	/* 
	   To avoid garbage collection issues with CanvasFontSet in C# preventing us from
	   immediately deleting the StorageFile, we shall do this here in C++ 
	   */
	CanvasFontSet^ fontset = ref new CanvasFontSet(uri);
	bool valid = fontset->Fonts->Size > 0;
	delete fontset;
	return valid;
}