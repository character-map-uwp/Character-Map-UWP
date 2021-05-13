// Copyright (c) Microsoft Corporation. All rights reserved.
//
// Licensed under the MIT License.
//  
// src: https://github.com/microsoft/Win2D
// See LICENSE.txt in the Win2D project root for license information.


#pragma once

#include "pch.h"
#include "Singleton.h"

namespace CharacterMapCX {
    class DefaultCustomFontManagerAdapter;
    
    class CustomFontManagerAdapter : public Singleton<CustomFontManagerAdapter, DefaultCustomFontManagerAdapter>
    {
    public:
        virtual ~CustomFontManagerAdapter() = default;
        virtual ComPtr<IDWriteFactory> CreateDWriteFactory(DWRITE_FACTORY_TYPE type) = 0;
    };
    
    class DefaultCustomFontManagerAdapter : public CustomFontManagerAdapter
    {
    public:
        virtual ComPtr<IDWriteFactory> CreateDWriteFactory(DWRITE_FACTORY_TYPE type) override;
    };
    
    class CustomFontManager : public Singleton<CustomFontManager>
    {
        std::shared_ptr<CustomFontManagerAdapter> m_adapter;
    
        std::recursive_mutex m_mutex;
        ComPtr<IDWriteFactory7> m_isolatedFactory;
        ComPtr<IDWriteFactory7> m_sharedFactory;
        ComPtr<IDWriteFontCollectionLoader> m_customLoader;
        ComPtr<IDWriteTextAnalyzer2> m_textAnalyzer;
        ComPtr<IDWriteFontFallback> m_systemFontFallback;
    
    public:
        CustomFontManager(ComPtr<IDWriteFactory7> sharedFactory);
    
        ComPtr<IDWriteFontCollection3> GetFontCollectionFromFile(StorageFile^ file);
    
    private:
        ComPtr<IDWriteFactory7> const& GetIsolatedFactory();
    
        __inline ComPtr<IDWriteFontCollection3> GetFontCollectionFromPath(Platform::String^ path);
    };
}