#pragma once

void TerminateApplicationWithError(int errorLevel , wchar_t* defaultString );

size_t SafeStringLengthInCharacters(const wchar_t* text ) {
	size_t stringLength;

	if( SUCCEEDED( StringCchLengthW(text, BUFSIZE, &stringLength )) ) {
		return stringLength;
	}
	return -1;
}

BOOL IsNullOrEmpty(const wchar_t* text) {
	return !( text && *text );
}

wchar_t* NewString() {
	wchar_t* result = (wchar_t*) malloc(BUFSIZE*sizeof(wchar_t));
	ZeroMemory(result, BUFSIZE*sizeof(wchar_t));
	return result;
}

void DeleteString(wchar_t** stringPointer ) {
	if( stringPointer )  {
		if( *stringPointer ) {
			free( *stringPointer );
		}
		*stringPointer = NULL;
	}
}

wchar_t* DuplicateString( const wchar_t* text ) {
	size_t size;
	wchar_t* result = NULL;
	
	if( IsNullOrEmpty(text ) ) {
		return NewString();
	}
	
	size = SafeStringLengthInCharacters(text);
	
	result = NewString();
	wcsncpy_s(result , BUFSIZE, text, size );

	return result;
}


wchar_t* Sprintf(const wchar_t* format, ... ) {
	wchar_t* result = NewString();
	va_list args;
	
	if( IsNullOrEmpty(format) ) {
		return NewString(); 
	}

	va_start(args, format);
	
	if( SUCCEEDED(StringCbVPrintf(result,BUFSIZE,format, args) ) ) {
		va_end(args);
		return result;	
	}
	return NULL;
}


void _DebugPrintf(const wchar_t* format, ...) {
    // Had to remove underscore in valist & vastart for formatting
	wchar_t* result = NewString();
	va_list args;
	
	va_start(args, format);
	
	if( SUCCEEDED(StringCbVPrintf(result,BUFSIZE,format, args) ) ) {
		va_end(args);
		OutputDebugString(result); 
	}
}

#define DebugPrintf(format, ... ) _DebugPrintf(L" [%s] =» [%d] %s", __WFUNCTION__, __LINE__ , Sprintf( format, __VA_ARGS__ ) );


const wchar_t* GetString( UINT resourceId, const wchar_t* defaultString ) {
	wchar_t* result = NewString();
	LoadString(resourceModule, resourceId , result , BUFSIZE); 
	if( IsNullOrEmpty(result) ) {
		DeleteString(&result);
		return defaultString;
	}
	return result;
}