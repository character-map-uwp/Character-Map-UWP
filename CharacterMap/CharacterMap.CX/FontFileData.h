#pragma once

using namespace Platform;
using namespace Windows::Storage::Streams;
using namespace Windows::Foundation::Collections;

namespace CharacterMapCX
{
	public ref class FontFileData sealed
	{
	public:

		property IBuffer^ Buffer
		{
			IBuffer^ get() { return m_buffer; }
		}

		property String^ FileName
		{
			String^ get() { return m_name;; }
		}

		virtual ~FontFileData()
		{
			m_buffer = nullptr;
			m_name = nullptr;
		}

	internal:
		FontFileData(IBuffer^ buffer, String^ name)
		{
			m_buffer = buffer;
			m_name = name;
		}

	private:
		inline FontFileData() { }

		IBuffer^ m_buffer = nullptr;
		String^ m_name = nullptr;
	};
}