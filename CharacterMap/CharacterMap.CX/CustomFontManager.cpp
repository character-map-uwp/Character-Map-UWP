// Copyright (c) Microsoft Corporation. All rights reserved.
//
// Licensed under the MIT License.
//  
// src: https://github.com/microsoft/Win2D
// See LICENSE.txt in the Win2D project root for license information.

#pragma once

#include "pch.h"
#include "CustomFontManager.h"
#include <robuffer.h>

using namespace CharacterMapCX;


ComPtr<IDWriteFactory> DefaultCustomFontManagerAdapter::CreateDWriteFactory(DWRITE_FACTORY_TYPE type)
{
    ComPtr<IDWriteFactory> factory;
    ThrowIfFailed(DWriteCreateFactory(type, __uuidof(factory), &factory));
    return factory;
}

class CustomFontFileEnumerator
    : public RuntimeClass<RuntimeClassFlags<ClassicCom>, IDWriteFontFileEnumerator>
    , private LifespanTracker<CustomFontFileEnumerator>
{
    ComPtr<IDWriteFactory> m_factory;
    std::wstring m_filename;
    ComPtr<IDWriteFontFile> m_theFile;

public:
    CustomFontFileEnumerator(IDWriteFactory* factory, void const* collectionKey, uint32_t collectionKeySize)
        : m_factory(factory)
        , m_filename(static_cast<wchar_t const*>(collectionKey), collectionKeySize / 2)
    {
    }

    IFACEMETHODIMP MoveNext(BOOL* hasCurrentFile) override
    {
        if (m_theFile)
        {
            *hasCurrentFile = FALSE;
        }
        else if (SUCCEEDED(m_factory->CreateFontFileReference(m_filename.c_str(), nullptr, &m_theFile)))
        {
            *hasCurrentFile = TRUE;
        }
        else
        {
            *hasCurrentFile = FALSE;
        }

        return S_OK;
    }

    IFACEMETHODIMP GetCurrentFontFile(IDWriteFontFile** fontFile) override
    {
        return m_theFile.CopyTo(fontFile);
    }
};


class CustomFontLoader
    : public RuntimeClass<RuntimeClassFlags<ClassicCom>, IDWriteFontCollectionLoader>
    , private LifespanTracker<CustomFontLoader>
{
public:
    IFACEMETHODIMP CreateEnumeratorFromKey(
        IDWriteFactory* factory,
        void const* collectionKey,
        uint32_t collectionKeySize,
        IDWriteFontFileEnumerator** fontFileEnumerator) override
    {
        return ExceptionBoundary(
            [=]
            {
                auto enumerator = Make<CustomFontFileEnumerator>(factory, collectionKey, collectionKeySize);
                CheckMakeResult(enumerator);
                ThrowIfFailed(enumerator.CopyTo(fontFileEnumerator));
            });
    }
};


CustomFontManager::CustomFontManager() : m_adapter(CustomFontManagerAdapter::GetInstance())
{
    // DEATH;
}

CustomFontManager::CustomFontManager(ComPtr<IDWriteFactory7> sharedFactory)
    : m_adapter(CustomFontManagerAdapter::GetInstance())
{
    m_sharedFactory = sharedFactory;
}

//ComPtr<IDWriteFontCollection3> CustomFontManager::GetFontCollectionFromFile(StorageFile^ file)
//{
//    auto path = file->Path;
//    return GetFontCollectionFromPath(path);
//}

ComPtr<IDWriteFontCollection3> CustomFontManager::GetFontCollection(Platform::String^ path)
{
    auto pathBegin = begin(path);
    auto pathEnd = end(path);

    assert(pathBegin && pathEnd);

    void const* key = pathBegin;
    uint32_t keySize = static_cast<uint32_t>(std::distance(pathBegin, pathEnd) * sizeof(wchar_t));

    ComPtr<IDWriteFontCollection> tcollection;
    ComPtr<IDWriteFontCollection3> collection;

    auto& factory = GetIsolatedFactory();
    ThrowIfFailed(factory->CreateCustomFontCollection(m_customLoader.Get(), key, keySize, &tcollection));

    tcollection.As<IDWriteFontCollection3>(&collection);

    return collection;
}

ComPtr<IDWriteFactory7> const& CustomFontManager::GetIsolatedFactory()
{
    RecursiveLock lock(m_mutex);

    if (!m_isolatedFactory)
    {
        auto fac = m_adapter->CreateDWriteFactory(DWRITE_FACTORY_TYPE_ISOLATED);
        fac.As<IDWriteFactory7>(&m_isolatedFactory);

        m_customLoader = Make<CustomFontLoader>();
        ThrowIfFailed(m_isolatedFactory->RegisterFontCollectionLoader(m_customLoader.Get()));
    }

    return m_isolatedFactory;
}
