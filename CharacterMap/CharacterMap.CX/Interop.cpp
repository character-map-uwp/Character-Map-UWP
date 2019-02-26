#include "pch.h"
#include "Interop.h"

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

	m_colorAnalyzer = new (std::nothrow) ColorTextAnalyzer(m_d2dFactory, m_dwriteFactory, m_d2dContext);
}

bool Interop::HasColorGlyphs(CanvasTextLayout^ layout)
{
	ComPtr<IDWriteTextLayout3> context = GetWrappedResource<IDWriteTextLayout3>(layout);
	context->Draw(m_d2dContext.Get(), m_colorAnalyzer.Get(), 0, 0);
	return  m_colorAnalyzer->HasColorGlyphs;
}