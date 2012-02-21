//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Copyright (c) 2011 Garrett Serack . All rights reserved.
// </copyright>
// <license>
//     The software is licensed under the Apache 2.0 License (the "License")
//     You may not use the software except in compliance with the License. 
// </license>
//-----------------------------------------------------------------------

#pragma once
void SetProgressValue( int overallprogress );

///
/// <summary> 
///		combines a path and a filename
/// </summary>
 wchar_t* UrlOrPathCombine(const wchar_t* path, const wchar_t* name, wchar_t seperator) {
	if( IsNullOrEmpty(path) && IsNullOrEmpty(name) ) {
		 return NewString();
	}

	if( IsNullOrEmpty(path) ){
		 return DuplicateString(name);
	}

	if( IsNullOrEmpty(name) ){
		 return DuplicateString(path);
	}

	if( path[SafeStringLengthInCharacters( path )-1] == seperator  ) {
		return Sprintf( L"%s%s" , path, name );
	}
	return Sprintf(L"%s%c%s" , path, seperator, name );
}

 
///
/// <summary> 
///		creates a temporary name for a file 
///		caller must free the memory for the string returned.
///		returns NULL on error.
/// </summary>
wchar_t* UniqueTempFileName(const wchar_t* name,const wchar_t* extension) {
	DWORD returnValue = 0;
	wchar_t tempFolderPath[BUFSIZE];
	wchar_t* filename = NULL;
	wchar_t* result = NULL;

	returnValue = GetTempPath(BUFSIZE,  tempFolderPath); 
	
	if (returnValue > BUFSIZE || (returnValue == 0)) {
		free( filename );
		return NULL;
	}

	filename = Sprintf(L"%s[%d].%s", name , GetTickCount(), extension );

	result = UrlOrPathCombine(tempFolderPath, filename, L'\\' );
	DeleteString( &filename );
	return result;
}

wchar_t* TempFileName(const wchar_t* name) {
	DWORD returnValue = 0;
	wchar_t tempFolderPath[BUFSIZE];

	returnValue = GetTempPath(BUFSIZE,  tempFolderPath); 
	
	if (returnValue > BUFSIZE || (returnValue == 0)) {
		return NULL;
	}

	return UrlOrPathCombine(tempFolderPath, name, L'\\' );
}


// given a path, returns the folder that contains it.
wchar_t* GetFolderFromPath( const wchar_t* path ) {
	wchar_t* result = DuplicateString(path);
	wchar_t* position = NULL;
	int length= wcslen(result);

	position = result+length;
	while( position >= result && position[0] != L'\\')
		position--;
	position[1] = 0;

	return result;
}

const wchar_t* GetFilenameFromPath( const wchar_t* path ) {
	const wchar_t* position = NULL;
	int length= wcslen(path);

	position = path+length;
	while( position >= path && position[0] != L'\\')
		position--;

	return position+1;
}

BOOL FileExists(const wchar_t* filePath) {
    WIN32_FILE_ATTRIBUTE_DATA fileData;

    if( IsNullOrEmpty(filePath) ) {
        return 0;
	}

    return GetFileAttributesEx( filePath, GetFileExInfoStandard, &fileData);
}

BOOL IsEmbeddedSignatureValid(LPCWSTR pwszSourceFile)
{
    LONG lStatus;
    DWORD dwLastError;

    // Initialize the WINTRUST_FILE_INFO structure.

    WINTRUST_FILE_INFO FileData;
    GUID WVTPolicyGUID = WINTRUST_ACTION_GENERIC_VERIFY_V2;
    WINTRUST_DATA WinTrustData;

	if( !FileExists(pwszSourceFile) )
		return FALSE;

#ifdef _DEBUG
	return TRUE;
#endif

    memset(&FileData, 0, sizeof(FileData));
    FileData.cbStruct = sizeof(WINTRUST_FILE_INFO);
    FileData.pcwszFilePath = pwszSourceFile;
    FileData.hFile = NULL;
    FileData.pgKnownSubject = NULL;

    /*
    WVTPolicyGUID specifies the policy to apply on the file
    WINTRUST_ACTION_GENERIC_VERIFY_V2 policy checks:
    
    1) The certificate used to sign the file chains up to a root 
    certificate located in the trusted root certificate store. This 
    implies that the identity of the publisher has been verified by 
    a certification authority.
    
    2) In cases where user interface is displayed (which this example
    does not do), WinVerifyTrust will check for whether the  
    end entity certificate is stored in the trusted publisher store,  
    implying that the user trusts content from this publisher.
    
    3) The end entity certificate has sufficient permission to sign 
    code, as indicated by the presence of a code signing EKU or no 
    EKU.
    */


    // Initialize the WinVerifyTrust input data structure.

    // Default all fields to 0.
	ZeroMemory(&WinTrustData, sizeof(WinTrustData));
    WinTrustData.cbStruct = sizeof(WinTrustData);
    
    // Use default code signing EKU.
    WinTrustData.pPolicyCallbackData = NULL;

    // No data to pass to SIP.
    WinTrustData.pSIPClientData = NULL;

    // Disable WVT UI.
    WinTrustData.dwUIChoice = WTD_UI_NONE;

    // No revocation checking.
    WinTrustData.fdwRevocationChecks = WTD_REVOKE_NONE; 

    // Verify an embedded signature on a file.
    WinTrustData.dwUnionChoice = WTD_CHOICE_FILE;

    // Default verification.
    WinTrustData.dwStateAction = 0;

    // Not applicable for default verification of embedded signature.
    WinTrustData.hWVTStateData = NULL;

    // Not used.
    WinTrustData.pwszURLReference = NULL;

    // Default.
    WinTrustData.dwProvFlags = WTD_SAFER_FLAG;

    // This is not applicable if there is no UI because it changes 
    // the UI to accommodate running applications instead of 
    // installing applications.
    WinTrustData.dwUIContext = 0;

    // Set pFile.
    WinTrustData.pFile = &FileData;

    // WinVerifyTrust verifies signatures as specified by the GUID 
    // and Wintrust_Data.
    lStatus = WinVerifyTrust( NULL, &WVTPolicyGUID, &WinTrustData);

    switch (lStatus) {
        case ERROR_SUCCESS:
            /*
            Signed file:
                - Hash that represents the subject is trusted.

                - Trusted publisher without any verification errors.

                - UI was disabled in dwUIChoice. No publisher or 
                    time stamp chain errors.

                - UI was enabled in dwUIChoice and the user clicked 
                    "Yes" when asked to install and run the signed 
                    subject.
            */
            // wprintf_s(L"The file \"%s\" is signed and the signature was verified.\n", pwszSourceFile);
			return TRUE;
            break;
        
        case TRUST_E_NOSIGNATURE:
            // The file was not signed or had a signature 
            // that was not valid.

            // Get the reason for no signature.
            dwLastError = GetLastError();
            if (TRUST_E_NOSIGNATURE == dwLastError || TRUST_E_SUBJECT_FORM_UNKNOWN == dwLastError || TRUST_E_PROVIDER_UNKNOWN == dwLastError) {
                // The file was not signed.
                // wprintf_s(L"The file \"%s\" is not signed.\n pwszSourceFile);
            }  else {
                // The signature was not valid or there was an error 
                // opening the file.
               //  wprintf_s(L"An unknown error occurred trying to  verify the signature of the \"%s\" file.\n", pwszSourceFile);
            }

            break;

        case TRUST_E_EXPLICIT_DISTRUST:
            // The hash that represents the subject or the publisher 
            // is not allowed by the admin or user.
            // wprintf_s(L"The signature is present, but specifically disallowed.\n");
            break;

        case TRUST_E_SUBJECT_NOT_TRUSTED:
            // The user clicked "No" when asked to install and run.
            // wprintf_s(L"The signature is present, but not trusted.\n");
            break;

        case CRYPT_E_SECURITY_SETTINGS:
            /*
            The hash that represents the subject or the publisher 
            was not explicitly trusted by the admin and the 
            admin policy has disabled user trust. No signature, 
            publisher or time stamp errors.
            */
            // wprintf_s(L"CRYPT_E_SECURITY_SETTINGS - The hash representing the subject or the publisher wasn't explicitly trusted by the admin and admin policy has disabled user trust. No signature, publisher or timestamp errors.\n");
            break;

        default:
            // The UI was disabled in dwUIChoice or the admin policy 
            // has disabled user trust. lStatus contains the 
            // publisher or time stamp chain error.
            // wprintf_s(L"Error is: 0x%x.\n", lStatus);
            break;
    }
    return FALSE;
}

#define DOWNLOAD_FAIL_CANCELLED			 -12
#define DOWNLOAD_FAIL_ALLOCATION_FAILURE -11
#define DOWNLOAD_FAIL_NO_DATA_AVAILABLE -10
#define DOWNLOAD_FAIL_CREATING_FILE		-9
#define DOWNLOAD_FAIL_NOT_200_OK		-8
#define DOWNLOAD_FAIL_NO_RESPONSE		-7
#define DOWNLOAD_FAIL_SEND_REQUEST		-6
#define DOWNLOAD_FAIL_OPENING_REQUEST   -5
#define DOWNLOAD_FAIL_CANT_CONNECT		-4
#define DOWNLOAD_FAIL_NO_CONNECTION		-3
#define DOWNLOAD_FAIL_BAD_URL			-2
#define DOWNLOAD_FAIL_404				-1
#define DOWNLOAD_SUCCESS				0
#define DOWNLOAD_PROGRESS				1

///
/// <summary> 
///		Downloads a file from a URL 
///		returns file size on success, -1 on error.
/// </summary>
int DownloadFile(const wchar_t* URL, const wchar_t* destinationFilename) {
	URL_COMPONENTS urlComponents;

	wchar_t urlPath[BUFSIZE];
	wchar_t urlHost[BUFSIZE];
	void* pszOutBuffer;

	HINTERNET  session = NULL;
	HINTERNET  connection = NULL;
	HINTERNET  request = NULL;
	DWORD bytesDownloaded = 0;
	DWORD bytesAvailable = 0;
	DWORD bytesWritten = 0;
	DWORD dwStatusCode = 0;
	DWORD contentLength;
	__int64 totalBytesDownloaded = 0;
	DWORD tmpValue= 0;
	HANDLE localFile = NULL;
	int percentComplete =0;
	
	DebugPrintf(L"HTTP GET: [%s]",URL);

	__try {
		ZeroMemory(&urlComponents, sizeof(urlComponents));
		urlComponents.dwStructSize = sizeof(urlComponents);

		urlComponents.dwSchemeLength    = -1;
		urlComponents.dwHostNameLength  = -1;
		urlComponents.dwUrlPathLength   = -1;
		urlComponents.dwExtraInfoLength = -1;

		if(!WinHttpCrackUrl(URL, (DWORD)wcslen(URL), 0, &urlComponents)) {
			totalBytesDownloaded = DOWNLOAD_FAIL_BAD_URL;
			__leave;
		}

		wcsncpy_s( urlHost , BUFSIZE, URL+urlComponents.dwSchemeLength+3 ,urlComponents.dwHostNameLength );
		wcsncpy_s( urlPath , BUFSIZE, URL+urlComponents.dwSchemeLength+urlComponents.dwHostNameLength+3, urlComponents.dwUrlPathLength );

		// Use WinHttpOpen to obtain a session handle.
		if(!(session = WinHttpOpen( L"CoAppBootstrapper/1.0",  WINHTTP_ACCESS_TYPE_DEFAULT_PROXY,WINHTTP_NO_PROXY_NAME, WINHTTP_NO_PROXY_BYPASS, 0))) {
			totalBytesDownloaded = DOWNLOAD_FAIL_NO_CONNECTION;
			__leave;
		}

		WinHttpSetTimeouts( session, 6000, 12000, 12000, 12000);

		// Specify an HTTP server.
		if (!(connection = WinHttpConnect( session, urlHost, urlComponents.nPort, 0))) {
			totalBytesDownloaded = DOWNLOAD_FAIL_CANT_CONNECT;
			__leave;
		}

		// Create an HTTP request handle.
		if (!(request = WinHttpOpenRequest( connection, L"GET",urlPath , NULL, WINHTTP_NO_REFERER,  WINHTTP_DEFAULT_ACCEPT_TYPES, 0))) {
			totalBytesDownloaded = DOWNLOAD_FAIL_OPENING_REQUEST;
			__leave;
		}

		// Send a request.
		if(!(WinHttpSendRequest( request, WINHTTP_NO_ADDITIONAL_HEADERS, 0, WINHTTP_NO_REQUEST_DATA, 0, 0, 0))) {
			totalBytesDownloaded = DOWNLOAD_FAIL_SEND_REQUEST;
			__leave;
		}
 
		// End the request.
		if(!(WinHttpReceiveResponse( request, NULL))) {
			totalBytesDownloaded = DOWNLOAD_FAIL_NO_RESPONSE;
			__leave;		
		}

		tmpValue = sizeof(DWORD);
		WinHttpQueryHeaders( request, WINHTTP_QUERY_STATUS_CODE| WINHTTP_QUERY_FLAG_NUMBER, NULL, &dwStatusCode, &tmpValue, NULL );
		if( dwStatusCode != HTTP_STATUS_OK ) {
			totalBytesDownloaded = DOWNLOAD_FAIL_NOT_200_OK;
			__leave;		
		}

		tmpValue = sizeof(DWORD);
		WinHttpQueryHeaders( request, WINHTTP_QUERY_CONTENT_LENGTH | WINHTTP_QUERY_FLAG_NUMBER, NULL, &contentLength, &tmpValue , NULL);

		if( INVALID_HANDLE_VALUE == (localFile = CreateFile(destinationFilename, GENERIC_WRITE, 0, NULL, CREATE_ALWAYS,  FILE_ATTRIBUTE_NORMAL,NULL))) {
			totalBytesDownloaded = DOWNLOAD_FAIL_CREATING_FILE;
			__leave;		
		}

		// Allocate space for the buffer.
		pszOutBuffer = malloc(128*1024); // 128k buffer should be fine.
		if (!pszOutBuffer)  {
			totalBytesDownloaded = DOWNLOAD_FAIL_ALLOCATION_FAILURE;
			__leave;
		}
	
		// Keep checking for data until there is nothing left.
		do  {
			if( IsShuttingDown ) {
				totalBytesDownloaded = DOWNLOAD_FAIL_CANCELLED;
				__leave;
			}

			// Check for available data.
			bytesAvailable = 0;

			if (!WinHttpQueryDataAvailable( request, &bytesAvailable)) {
				totalBytesDownloaded = DOWNLOAD_FAIL_NO_DATA_AVAILABLE;
				__leave;
			}
			// No more available data.
			if (!bytesAvailable)
				break;

			if (!WinHttpReadData( request, (LPVOID)pszOutBuffer, 128*1024, &bytesDownloaded))  {
				totalBytesDownloaded = DOWNLOAD_FAIL_ALLOCATION_FAILURE;
				__leave;
			}
		
			WriteFile( localFile, pszOutBuffer, bytesDownloaded, &bytesWritten, NULL ); 
			totalBytesDownloaded+=bytesDownloaded;

			// we really don't support progress for this anymore.
			// percentComplete = (int)(totalBytesDownloaded*100/contentLength );
			// OnDownloadProgress( DOWNLOAD_PROGRESS , percentComplete);

			// This condition should never be reached since WinHttpQueryDataAvailable
			// reported that there are bits to read.
			if (!bytesDownloaded)
				break;
				
		} while (bytesAvailable > 0);
	} __finally { 
		if( pszOutBuffer ) 
			free(pszOutBuffer); // Free the memory allocated to the buffer.
			
		// Close open handles.
		if (localFile)
			CloseHandle( localFile );
		if (request) 
			WinHttpCloseHandle(request);
		if (connection) 
			WinHttpCloseHandle(connection);
		if (session) 
			WinHttpCloseHandle(session);
	}

	return (int)totalBytesDownloaded; // bytes downloaded.
}

wchar_t* DownloadRelativeFile( const wchar_t* baseUrl, const wchar_t* filename) {
	wchar_t* result = NULL;
	wchar_t* url = NULL;

	__try {
		if( !IsNullOrEmpty(baseUrl)) {
			result = TempFileName(filename);
			url = UrlOrPathCombine( baseUrl , filename, '/' );
			
			if( DownloadFile( url, result) > 0 && FileExists(result) ) {
				if(IsEmbeddedSignatureValid( result ) ) {
					__leave;
				}
				DeleteFile( result );
			}
			DeleteString(&result);
		}
	} __finally {
		DeleteString(&url);
	}

	return result;
}

wchar_t* GetExtension(const wchar_t* filename) {
	int i;
	
	for(i=SafeStringLengthInCharacters(filename);i>=0;i--) {
		if( filename[i] == '.' ) {
			return DuplicateString(filename+i+1);
		}
	}

	return NULL;
}

wchar_t* GetFilenameWithoutExtension(const wchar_t* filename) {
	wchar_t* result;
	int i;
	
	result = DuplicateString(filename);
	for(i=SafeStringLengthInCharacters(result);i>=0;i--) {
		if( result[i] == '.' ) {
			result[i] =0;
			break;
		}
	}
	return result;
}

wchar_t* ExtractFileFromMSI( const wchar_t* msiFilename, const wchar_t* binaryFile ) {
	MSIHANDLE packageDatabase= 0;
	MSIHANDLE view = 0;
	MSIHANDLE record = 0;
	DWORD bufferSize = 0;
	char* byteBuffer = NULL;
	HANDLE localFile = NULL;
	wchar_t* query = NULL;
	DWORD bytesWritten = 0;

	wchar_t* result = NULL;
	
	if( IsNullOrEmpty(msiFilename) ) {
		return NULL;
	}

	__try { 
		if( ERROR_SUCCESS != MsiOpenDatabase(msiFilename, MSIDBOPEN_READONLY, &packageDatabase) ) {
			__leave;
		}

		query = Sprintf( L"SELECT `Data` FROM `Binary` where `Name`='%s'", binaryFile );

		if (ERROR_SUCCESS != MsiDatabaseOpenView(packageDatabase, query, &view)) {
			__leave;
		}
		if( ERROR_SUCCESS != MsiViewExecute(view, 0) ) {
			__leave;
		}
		if( ERROR_SUCCESS != MsiViewFetch(view, &record) ) {
			__leave;
		}

		bufferSize = MsiRecordDataSize(record, 1);
		if( bufferSize > 1024*1024*1024 || bufferSize == 0 ) {  //bigger than 1Meg?
			__leave;
		}

		byteBuffer = (char*)malloc(bufferSize);
		
		if( ERROR_SUCCESS != MsiRecordReadStream(record, 1, byteBuffer, &bufferSize) ) {
			__leave;
		}

		// got the whole file
		result = TempFileName(binaryFile);
		if( INVALID_HANDLE_VALUE == (localFile = CreateFile(result, GENERIC_WRITE, 0, NULL, CREATE_ALWAYS,  FILE_ATTRIBUTE_NORMAL,NULL))) {
			DeleteString(&result);
			__leave;
		}

		// write out the file to the temp file.
		WriteFile( localFile, byteBuffer, bufferSize, &bytesWritten, NULL ); 
		CloseHandle( localFile );
	} __finally { 
		if ( record ) 
			MsiCloseHandle(record);
		if ( view ) 
			MsiCloseHandle(view);
		if ( packageDatabase ) 
			MsiCloseHandle(packageDatabase);

		DeleteString(&query);

		if( byteBuffer ) {
			free( (void*) byteBuffer );
		}
	}
    return result;
}

// This gets a dependent resource, by finding it in one of the following locations
//		same folder as the bootstrap.exe
//		embedded (and unpacked from) the MSI
//		http://coapp.org/resources/<filename>.<LCID>.<ext>
//		http://coapp.org/resources/<filename>.<ext>
wchar_t* AcquireFile( const wchar_t* filename, BOOL searchOnline, const wchar_t* additionalDownloadServer ) {
	LCID lcid;
	// wchar_t* folder = NULL;
	wchar_t* extension= NULL;
	wchar_t* result= NULL;
	wchar_t* name= NULL;
	wchar_t* localizedFilename  = NULL;
	wchar_t* url = NULL;

	if( IsNullOrEmpty(filename) ) {
		return NULL;
	}
	__try {
		// split the filename parts
		lcid = GetUserDefaultLCID();
		name = GetFilenameWithoutExtension(filename);
		extension = GetExtension(filename);
		localizedFilename = Sprintf(L"%s.%d.%s", name, lcid, extension);

		//------------------------
		// LOCALIZED FILE, ON BOX
		//------------------------
		
		// is the localized file in the bootstrap folder?
		result = UrlOrPathCombine( BootstrapFolder, localizedFilename, L'\\');
		DebugPrintf(L"Trying %s", result );
		if( FileExists( result ) && IsEmbeddedSignatureValid(result) ) {
			__leave; // found it 
		}
		DeleteString(&result);

		// is the localized file in the msi folder?
		result = UrlOrPathCombine( MsiFolder, localizedFilename, L'\\');
		DebugPrintf(L"Trying %s", result );
		if( FileExists( result ) && IsEmbeddedSignatureValid(result) ) {
			__leave; // found it 
		}
		DeleteString(&result);

		// try the MSI for the localized file 
		result = ExtractFileFromMSI( MsiFile, localizedFilename );
		DebugPrintf(L"Trying %s::%s", MsiFile, localizedFilename );
		if( FileExists( result ) && IsEmbeddedSignatureValid(result) ) {
			__leave; // found it 
		}
		DeleteString(&result);

		//------------------------
		// NORMAL FILE, ON BOX
		//------------------------

		// is the standard file in the bootstrap folder?
		result = UrlOrPathCombine( MsiFolder, filename, L'\\');
		DebugPrintf(L"Trying %s", result );
		if( FileExists( result ) && IsEmbeddedSignatureValid(result) ) {
			__leave; // found it 
		}
		DeleteString(&result);

		// is the standard file in the msi folder?
		result = UrlOrPathCombine( BootstrapFolder, filename, L'\\');
		DebugPrintf(L"Trying %s", result );
		if( FileExists( result ) && IsEmbeddedSignatureValid(result) ) {
			__leave; // found it 
		}
		DeleteString(&result);

		// try the MSI for the regular file 
		result = ExtractFileFromMSI( MsiFile, filename );
		DebugPrintf(L"Trying %s::%s", MsiFile, filename );
		if( FileExists( result ) && IsEmbeddedSignatureValid(result) ) {
			__leave; // found it 
		}
		DeleteString(&result);

		if( !searchOnline ) {
			__leave; // aint gonna find it.
		}

		if( !IsNullOrEmpty(additionalDownloadServer) ) {
			// try regular file off the bootstrap server
			DebugPrintf(L"Trying %s::%s", additionalDownloadServer, filename );
			result = DownloadRelativeFile( additionalDownloadServer, filename  );
			
			if( FileExists( result ) && IsEmbeddedSignatureValid(result) ) {
				__leave; // found it 
			}
			DeleteString(&result);
		}

		//------------------------
		// LOCALIZED FILE, REMOTE
		//------------------------

		// try localized file off the bootstrap server
		DebugPrintf(L"Trying %s::%s", BootstrapServerUrl, localizedFilename );
		result = DownloadRelativeFile( BootstrapServerUrl, localizedFilename);
		if( FileExists( result ) && IsEmbeddedSignatureValid(result) ) {
			__leave; // found it 
		}
		DeleteString(&result);

		// try localized file off the coapp server
		DebugPrintf(L"Trying %s::%s", CoAppServerUrl, localizedFilename );
		result = DownloadRelativeFile( CoAppServerUrl, localizedFilename );
		if( FileExists( result ) && IsEmbeddedSignatureValid(result) ) {
			__leave; // found it 
		}
		DeleteString(&result);

		//------------------------
		// NORMAL FILE, REMOTE
		//------------------------

		// try regular file off the bootstrap server
		DebugPrintf(L"Trying %s::%s", BootstrapServerUrl, filename );
		result = DownloadRelativeFile( BootstrapServerUrl, filename  );
		if( FileExists( result ) && IsEmbeddedSignatureValid(result) ) {
			__leave; // found it 
		}
		DeleteString(&result);

		// try regular file off the coapp server
		DebugPrintf(L"Trying %s::%s", CoAppServerUrl, filename );
		result = DownloadRelativeFile( CoAppServerUrl, filename);
		if( FileExists( result ) && IsEmbeddedSignatureValid(result) ) {
			__leave; // found it 
		}
		DeleteString(&result);
 
		// this file aint nowhere .. gonna return null
	} __finally { 
		DeleteString(&extension);
		DeleteString(&name);
		DeleteString(&url);
		DeleteString(&localizedFilename);
	}

	return result;
}
