#include "pch.h"
#include "TypographyAnalyzer.h"
#include <vector>
#include <mutex>

using namespace Microsoft::Graphics::Canvas::Text;
using namespace Microsoft::WRL;
using namespace Windows::Foundation::Collections;
using namespace Platform::Collections;
using namespace CharacterMapCX;

namespace
{
	class TextAnalysisSource : public Microsoft::WRL::RuntimeClass<
		Microsoft::WRL::RuntimeClassFlags<Microsoft::WRL::ClassicCom>,
		IDWriteTextAnalysisSource>
	{
		const WCHAR* m_text;
		UINT32 m_length;
	public:
		TextAnalysisSource(const WCHAR* text, UINT32 length) : m_text(text), m_length(length) {}

		IFACEMETHOD(GetTextAtPosition)(UINT32 textPosition, _Outptr_result_buffer_(*textLength) const WCHAR** textString, _Out_ UINT32* textLength) override
		{
			if (textPosition >= m_length)
			{
				*textString = nullptr;
				*textLength = 0;
				return S_OK;
			}
			*textString = m_text + textPosition;
			*textLength = m_length - textPosition;
			return S_OK;
		}

		IFACEMETHOD(GetTextBeforePosition)(UINT32 textPosition, _Outptr_result_buffer_(*textLength) const WCHAR** textString, _Out_ UINT32* textLength) override
		{
			*textString = nullptr;
			*textLength = 0;
			return S_OK;
		}

		virtual DWRITE_READING_DIRECTION STDMETHODCALLTYPE GetParagraphReadingDirection() override
		{
			return DWRITE_READING_DIRECTION_LEFT_TO_RIGHT;
		}

		IFACEMETHOD(GetLocaleName)(UINT32 textPosition, _Out_ UINT32* textLength, _Outptr_result_z_ const WCHAR** localeName) override
		{
			*localeName = L"";
			*textLength = m_length - textPosition;
			return S_OK;
		}

		IFACEMETHOD(GetNumberSubstitution)(UINT32 textPosition, _Out_ UINT32* textLength, _COM_Outptr_ IDWriteNumberSubstitution** numberSubstitution) override
		{
			*numberSubstitution = nullptr;
			*textLength = m_length - textPosition;
			return S_OK;
		}
	};

	class TextAnalysisSink : public Microsoft::WRL::RuntimeClass<
		Microsoft::WRL::RuntimeClassFlags<Microsoft::WRL::ClassicCom>,
		IDWriteTextAnalysisSink>
	{
	public:
		DWRITE_SCRIPT_ANALYSIS ScriptAnalysis{};
		IFACEMETHOD(SetScriptAnalysis)(UINT32, UINT32, _In_ const DWRITE_SCRIPT_ANALYSIS* scriptAnalysis) override
		{
			if (scriptAnalysis != nullptr)
				ScriptAnalysis = *scriptAnalysis;
			return S_OK;
		}
		IFACEMETHOD(SetLineBreakpoints)(UINT32, UINT32, _In_ const DWRITE_LINE_BREAKPOINT*) override { return S_OK; }
		IFACEMETHOD(SetBidiLevel)(UINT32, UINT32, UINT8, UINT8) override { return S_OK; }
		IFACEMETHOD(SetNumberSubstitution)(UINT32, UINT32, _In_ IDWriteNumberSubstitution*) override { return S_OK; }
	};

	ComPtr<IDWriteTextAnalyzer2> GetDirectWriteTextAnalyzer()
	{
		static ComPtr<IDWriteTextAnalyzer2> s_analyzer;
		static std::mutex s_mutex;
		std::lock_guard<std::mutex> lock(s_mutex);
		if (!s_analyzer)
		{
			ComPtr<IDWriteFactory> factory;
			if (SUCCEEDED(DWriteCreateFactory(DWRITE_FACTORY_TYPE_SHARED, __uuidof(IDWriteFactory), &factory)))
			{
				ComPtr<IDWriteTextAnalyzer> baseAnalyzer;
				if (SUCCEEDED(factory->CreateTextAnalyzer(&baseAnalyzer)))
				{
					baseAnalyzer.As<IDWriteTextAnalyzer2>(&s_analyzer);
				}
			}
		}
		return s_analyzer;
	}
}

bool TypographyAnalyzer::CheckTypographicFeature(
	DWriteFontFace^ fontFace,
	Platform::String^ text,
	DWriteTypographyFeatureName feature)
{
	if (fontFace == nullptr || text == nullptr || text->Length() == 0 || feature == DWriteTypographyFeatureName::None)
		return false;

	auto textAnalyzer = GetDirectWriteTextAnalyzer();
	if (!textAnalyzer)
		return false;

	auto face = fontFace->GetFontFace();
	if (!face)
		return false;

	auto source = Microsoft::WRL::Make<TextAnalysisSource>(text->Data(), text->Length());
	auto sink = Microsoft::WRL::Make<TextAnalysisSink>();
	if (FAILED(textAnalyzer->AnalyzeScript(source.Get(), 0, text->Length(), sink.Get())))
		return false;

	UINT32 maxGlyphs = text->Length() * 3 / 2 + 16;
	std::vector<UINT16> glyphIndices(maxGlyphs);
	std::vector<DWRITE_SHAPING_TEXT_PROPERTIES> textProps(text->Length());
	std::vector<DWRITE_SHAPING_GLYPH_PROPERTIES> glyphProps(maxGlyphs);
	std::vector<UINT16> clusterMap(text->Length());
	UINT32 actualGlyphCount = 0;

	HRESULT hr = textAnalyzer->GetGlyphs(
		text->Data(),
		text->Length(),
		face.Get(),
		FALSE,
		FALSE,
		&sink->ScriptAnalysis,
		nullptr,
		nullptr,
		nullptr,
		nullptr,
		0,
		maxGlyphs,
		clusterMap.data(),
		textProps.data(),
		glyphIndices.data(),
		glyphProps.data(),
		&actualGlyphCount);

	if (FAILED(hr) || actualGlyphCount == 0)
		return false;

	std::vector<UINT8> featureApplies(actualGlyphCount, 0);
	hr = textAnalyzer->CheckTypographicFeature(
		face.Get(),
		sink->ScriptAnalysis,
		nullptr,
		static_cast<DWRITE_FONT_FEATURE_TAG>(feature),
		actualGlyphCount,
		glyphIndices.data(),
		featureApplies.data());

	if (FAILED(hr))
		return false;

	for (UINT32 i = 0; i < actualGlyphCount; ++i)
	{
		if (featureApplies[i] != 0)
			return true;
	}

	return false;
}

IVectorView<DWriteTypographyFeatureName>^ TypographyAnalyzer::GetSupportedTypographicFeatures(
	DWriteFontFace^ fontFace,
	Platform::String^ text,
	IVectorView<DWriteTypographyFeatureName>^ features)
{
	auto result = ref new Vector<DWriteTypographyFeatureName>();
	if (fontFace == nullptr || text == nullptr || text->Length() == 0 || features == nullptr || features->Size == 0)
		return result->GetView();

	auto textAnalyzer = GetDirectWriteTextAnalyzer();
	if (!textAnalyzer)
		return result->GetView();

	auto face = fontFace->GetFontFace();
	if (!face)
		return result->GetView();

	auto source = Microsoft::WRL::Make<TextAnalysisSource>(text->Data(), text->Length());
	auto sink = Microsoft::WRL::Make<TextAnalysisSink>();
	if (FAILED(textAnalyzer->AnalyzeScript(source.Get(), 0, text->Length(), sink.Get())))
		return result->GetView();

	UINT32 maxGlyphs = text->Length() * 3 / 2 + 16;
	std::vector<UINT16> glyphIndices(maxGlyphs);
	std::vector<DWRITE_SHAPING_TEXT_PROPERTIES> textProps(text->Length());
	std::vector<DWRITE_SHAPING_GLYPH_PROPERTIES> glyphProps(maxGlyphs);
	std::vector<UINT16> clusterMap(text->Length());
	UINT32 actualGlyphCount = 0;

	HRESULT hr = textAnalyzer->GetGlyphs(
		text->Data(),
		text->Length(),
		face.Get(),
		FALSE,
		FALSE,
		&sink->ScriptAnalysis,
		nullptr,
		nullptr,
		nullptr,
		nullptr,
		0,
		maxGlyphs,
		clusterMap.data(),
		textProps.data(),
		glyphIndices.data(),
		glyphProps.data(),
		&actualGlyphCount);

	if (FAILED(hr) || actualGlyphCount == 0)
		return result->GetView();

	std::vector<UINT8> featureApplies(actualGlyphCount, 0);
	for (unsigned int f = 0; f < features->Size; ++f)
	{
		DWriteTypographyFeatureName feat = features->GetAt(f);
		if (feat == DWriteTypographyFeatureName::None)
			continue;

		std::fill(featureApplies.begin(), featureApplies.end(), 0);
		hr = textAnalyzer->CheckTypographicFeature(
			face.Get(),
			sink->ScriptAnalysis,
			nullptr,
			static_cast<DWRITE_FONT_FEATURE_TAG>(feat),
			actualGlyphCount,
			glyphIndices.data(),
			featureApplies.data());

		if (SUCCEEDED(hr))
		{
			for (UINT32 i = 0; i < actualGlyphCount; ++i)
			{
				if (featureApplies[i] != 0)
				{
					result->Append(feat);
					break;
				}
			}
		}
	}

	return result->GetView();
}
