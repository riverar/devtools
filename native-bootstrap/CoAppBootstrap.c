//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Copyright (c) 2010 Garrett Serack. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

#define _WIN32_WINNT _WIN32_WINNT_WS03 
#define WIN32_LEAN_AND_MEAN             // Exclude rarely-used stuff from Windows headers

#include <SDKDDKVer.h>
#include <windows.h>
#include <Shellapi.h>

#include <Msi.h>
#include <MsiQuery.h>
#include <winhttp.h>
#include <process.h>
#include <wchar.h>
#include <malloc.h>	
#include <winhttp.h>
#include <winbase.h>
#include <stdarg.h>
#include <Commctrl.h>
#include <Strsafe.h>
#include <ole2.h>
#include <OleCtl.h>

#include <Softpub.h>
#include <wincrypt.h>
#include <wintrust.h>
#include <Strsafe.h>

#include "..\\resources\\resource.h"

// defines

#define BUFSIZE 8192

#define WIDEN2(x) L ## x
#define WIDEN(x) WIDEN2(x)
#define __WFUNCTION__ WIDEN(__FUNCTION__)
#define SETPROGRESS			WM_USER+2

// Global Data -------------------------------------------------------------------------------------------------------------------------------------
const wchar_t* DotNetWebInstallerUrl = L"http://download.microsoft.com/download/1/B/E/1BE39E79-7E39-46A3-96FF-047F95396215/";
const wchar_t* DotNetWebInstallerFilename= L"dotNetFx40_Full_setup.exe";

const wchar_t* DotNetFullInstallerUrl = L"http://download.microsoft.com/download/9/5/A/95A9616B-7A37-4AF6-BC36-D6EA96C8DAAE/";
const wchar_t* DotNetFullInstallerFilename = L"dotNetFx40_Full_x86_x64.exe";

const wchar_t* dot_net_regkey = L"Software\\Microsoft\\NET Framework Setup\\NDP\\v4\\Full#Install";
const wchar_t* ManagedBootstrapFilename = L"managed_bootstrap.exe";
const wchar_t* eventName = L"/Global/coappbootstrapper";
const wchar_t* sectionName = L"coappbootstrapper";
const wchar_t* CoAppServerUrl = L"http://coapp.org/resources/";
const wchar_t* HelpUrl = L"http://coapp.org/help/"; 
const wchar_t* BootstrapServerUrl = NULL;
const wchar_t* BootstrapServerHelpUrl = NULL;

HANDLE ApplicationInstance = 0;
HANDLE WorkerThread = NULL;
unsigned WorkerThreadId = 0;
BOOL IsShuttingDown = FALSE;

wchar_t* BootstrapPath;
wchar_t* BootstrapFolder;
wchar_t* MsiFile = NULL;
wchar_t* MsiFolder = NULL;

HANDLE sectionHandle = NULL;
HANDLE eventHandle = NULL;
HCURSOR hand;
struct MmioDataStructure* mmioData = NULL;

HWND StatusDialog = NULL;
HWND errorDialog = NULL;
HWND logoControl = NULL;
BOOL Ready = FALSE;

HMODULE resourceModule;
HBITMAP background;
HBITMAP logo;
HICON circle;
HICON circle_light;
HICON ximg;
HICON ximg_light;
int ErrorLevel = 0;
// -------------------------------------------------------------------------------------------------------------------------------------------------


// GDI+ C Interface --------------------------------------------------------------------------------------------------------------------------------
typedef struct {
  UINT32        GdiplusVersion;
  void*			DebugEventCallback;
  BOOL          SuppressBackgroundThread;
  BOOL          SuppressExternalCodecs;
} GdiplusStartupInput;

int  __stdcall GdiplusStartup( void* *token, GdiplusStartupInput *input, UINT *output );
int  __stdcall GdiplusShutdown( UINT *token );
int  __stdcall GdipCreateBitmapFromStream(void* stream, void** pBitmap);
int  __stdcall GdipCreateHBITMAPFromBitmap(void* pBitmap, HBITMAP* bitmap, UINT argb );
int  __stdcall GdipGetImageWidth(void* pBitmap, UINT* W);
int  __stdcall GdipGetImageHeight(void* pBitmap, UINT* H);
// -------------------------------------------------------------------------------------------------------------------------------------------------

#include "coapp_string.h"
#include "coapp_file.h"

// MMIO data structure for .NET installer IPC
typedef struct MmioDataStructure {
    char m_downloadFinished;        // Is download done yet?
    char m_installFinished;         // Is installer operation done yet?
    char m_downloadAbort;           // Set to cause downloader to abort.
    char m_installAbort;            // Set to cause installer operation to abort.
    HRESULT m_hrDownloadFinished;   // HRESULT for download.
    HRESULT m_hrInstallFinished;    // HRESULT for installer operation.
    HRESULT m_hrInternalError;      // Internal error from MSI if applicable.
    WCHAR m_szCurrentItemStep[MAX_PATH];   // This identifies the windows installer step being executed if an error occurs while processing an MSI, for example, "Rollback".
    unsigned char m_downloadProgressSoFar; // Download progress 0 - 255 (0 to 100% done). 
    unsigned char m_installProgressSoFar;  // Install progress 0 - 255 (0 to 100% done).
    WCHAR m_szEventName[MAX_PATH];         // Event that chainer creates and chainee opens to sync communications.
};

void Cancel() {
    IsShuttingDown = TRUE;

    if (NULL != mmioData) {
		// set cancel flags if we have a chainer going.
		mmioData->m_downloadAbort= TRUE;
		mmioData->m_installAbort = TRUE;
    }
}

void Shutdown() {
	Cancel();
	PostQuitMessage(0);
}

void SetProgressValue( int overallprogress ) {
	if( overallprogress  > 288 ) {
		overallprogress = 288;
	}
	PostMessage(StatusDialog, SETPROGRESS, (WPARAM)(overallprogress ),0 );
	InvalidateRect(GetDlgItem(StatusDialog, IDC_PROGRESS2), NULL, FALSE );
	UpdateWindow(StatusDialog);
	Sleep(20);
}

void OwnerDraw( DRAWITEMSTRUCT* pdis) { 
	RECT rect;
	BOOL light = (pdis->itemState & ODS_SELECTED);
	
	rect = pdis->rcItem;
	DrawIconEx( pdis->hDC,rect.right-32,rect.bottom-32, light ? (HICON) ximg_light :(HICON) ximg , 32, 32, 0, NULL, DI_NORMAL );

	if( IDC_CANCEL == pdis->CtlID ) {
		SetTextColor( pdis->hDC, light ? RGB(128,128,128): RGB(0,0,0) );
		DrawText(pdis->hDC , GetString(IDS_CANCEL, L"Cancel"), -1 , &rect, DT_LEFT | DT_VCENTER | DT_SINGLELINE );
		DrawIconEx( pdis->hDC,rect.right-32,rect.bottom-32, light ? (HICON) circle_light :(HICON) circle , 32, 32, 0, NULL, DI_NORMAL );
	}
}

INT_PTR CALLBACK DialogProc (HWND hwnd,  UINT message, WPARAM wParam,  LPARAM lParam) {
	HDC staticControl;
	int a, b;
	switch (message) {

		case SETPROGRESS: 
			SendMessage( GetDlgItem( hwnd, IDC_PROGRESS2), PBM_SETPOS,  wParam, lParam );
		break;

		/*case WM_SETCURSOR:
			if( hwnd == errorDialog && (HWND)wParam == GetDlgItem( hwnd, IDC_STATIC1+53) ) {
				SetCursor(hand);
				return TRUE;
			}
			return TRUE;
			break;
			*/
		case WM_CTLCOLORBTN:
		case WM_CTLCOLORSTATIC: 
		case WM_CTLCOLOREDIT:
			
			staticControl = (HDC) wParam;
			if( hwnd == errorDialog && (lParam == (LPARAM)GetDlgItem( hwnd, IDC_X ) || lParam == (LPARAM)GetDlgItem( hwnd, IDC_CANCEL ))  ) {
				SetBkColor(staticControl, RGB(18,115,170));
				return (INT_PTR)CreateSolidBrush(RGB(18,115,170));
			}

			if( hwnd != errorDialog ) {
				if( lParam == (LPARAM)GetDlgItem( hwnd, IDC_STATICTEXT3 )  ) {
					SetBkMode(staticControl , TRANSPARENT );
				}

				if( lParam == (LPARAM)GetDlgItem( hwnd, IDC_CANCEL )  ) {
					SetBkColor(staticControl, RGB(255,255,255));
					return (INT_PTR)CreateSolidBrush(RGB(255,255,255));
				}
				return (INT_PTR)GetStockObject(NULL_BRUSH);
			} else {
				SetBkMode(staticControl , TRANSPARENT );
				SetTextColor(staticControl, RGB(255,255,255));
				//return (INT_PTR)CreateSolidBrush(RGB(255,255,255));
				return (INT_PTR)GetStockObject(NULL_BRUSH);
			}
			break;

		case WM_CTLCOLORDLG:
			if( hwnd == errorDialog ) {
				staticControl = (HDC) wParam;
				SetBkColor(staticControl, RGB(18,115,170));
				return (INT_PTR)CreateSolidBrush(RGB(18,115,170));
			}
			return (INT_PTR)GetStockObject(NULL_BRUSH);
			break;

		case WM_DESTROY:
			Shutdown();
			return TRUE;

		case WM_COMMAND:
			a = LOWORD(wParam);
			b = HIWORD(wParam);

			switch( a ) {
				case IDC_X: 
				case IDC_CANCEL: 
					if( hwnd == errorDialog ) {
						Ready = FALSE;
						Shutdown();
						return TRUE;
					}

					if( !Ready|| MessageBox(hwnd, GetString(IDS_OK_TO_CANCEL,  L"Are you sure you would like to cancel?"), GetString(IDS_CANCEL, L"Cancel"), MB_ICONEXCLAMATION | MB_YESNO ) == IDYES ) { 
						Ready = FALSE;
						// after they click, if we are still monitoring the installer, we really should 
						// wait for that to clean up (otherwise it keeps going.)
						hwnd = GetDlgItem(StatusDialog, IDC_STATICTEXT3);
						SetWindowText(hwnd, GetString(IDS_CANCELLING, L"Cancelling..."));
						InvalidateRect(hwnd, NULL, FALSE );
						UpdateWindow(StatusDialog);

						

						Cancel();
					}
					return TRUE;
				break;

				case IDC_STATIC1+53:
					ShellExecute(NULL, L"open", Sprintf( L"%s%d", HelpUrl, ErrorLevel) , NULL, NULL, SW_SHOWNORMAL); 
					break;
			}
			return TRUE;

		case WM_CLOSE:
			if( !Ready || MessageBox(hwnd, GetString(IDS_OK_TO_CANCEL,  L"Are you sure you would like to cancel?"), GetString(IDS_CANCEL, L"Cancel"), MB_ICONEXCLAMATION | MB_YESNO ) == IDYES ) { 
				Ready = FALSE;
				// after they click, if we are still monitoring the installer, we really should 
				// wait for that to clean up (otherwise it keeps going.)
				hwnd = GetDlgItem(StatusDialog, IDC_STATICTEXT3);
				SetWindowText(hwnd, GetString(IDS_CANCELLING, L"Cancelling..."));
				InvalidateRect(hwnd, NULL, FALSE );
				UpdateWindow(StatusDialog);

				Cancel();
			}
			return TRUE;

		case WM_INITDIALOG:
			return TRUE;
			break;

		case WM_NCHITTEST:
			SetWindowLong(hwnd,DWL_MSGRESULT,(LONG)HTCAPTION);
			return HTCAPTION;
			break;

		case WM_DRAWITEM:
			switch(((DRAWITEMSTRUCT*) lParam)->CtlID) {
				case IDC_CANCEL:
				case IDC_X:
					OwnerDraw(  (DRAWITEMSTRUCT*) lParam );
					break;

				default:
					break;
			}
			return(TRUE);
	}
	return FALSE;
}

void GrabBitmap(HMODULE module, int resourceId,  HBITMAP* phBitmap ) {
	HRSRC resource;
	UINT size;
	HGLOBAL imageBuffer;
	int result;
	HGLOBAL hGlobal;
	LPVOID pvData = NULL;
	void* pBitmap;
	LPSTREAM stream;

	resource = FindResource(module, MAKEINTRESOURCE(resourceId), L"BINARY"); 
	size = SizeofResource(module, resource); 
	imageBuffer = LoadResource(module, resource);

    hGlobal = GlobalAlloc(GMEM_MOVEABLE, size);
    pvData = GlobalLock(hGlobal);
	memcpy_s(pvData, size, imageBuffer, size);
    GlobalUnlock(hGlobal);

	CreateStreamOnHGlobal(hGlobal, TRUE, &stream);
	result =GdipCreateBitmapFromStream( stream, &pBitmap );
	result =GdipCreateHBITMAPFromBitmap( pBitmap, phBitmap, 0 );
	GlobalFree(hGlobal);
}

BOOL LoadResources(const wchar_t* resourceDll) {
	void* token;
	int result;
	GdiplusStartupInput gsi;

	ZeroMemory( &gsi, 16);
	gsi.GdiplusVersion = 1;
	result = GdiplusStartup( &token, &gsi, NULL);

	resourceModule = LoadLibraryEx(resourceDll, NULL,  LOAD_LIBRARY_AS_DATAFILE);
	if( resourceModule == NULL ) {
		return FALSE;
	}
	GrabBitmap(resourceModule, BACKGROUND_PNG, &background);
	GrabBitmap(resourceModule, LOGO_PNG, &logo);

	circle = (HICON) LoadIcon(resourceModule, MAKEINTRESOURCE(CIRCLE_ICO));
	circle_light = (HICON) LoadIcon(resourceModule, MAKEINTRESOURCE(CIRCLE_LIGHT_ICO));
	ximg = (HICON) LoadIcon(resourceModule, MAKEINTRESOURCE(X_ICO));
	ximg_light = (HICON) LoadIcon(resourceModule, MAKEINTRESOURCE(X_LIGHT_ICO));

	return TRUE;
}

int ShowGUI( HINSTANCE hInstance ) {
	MSG  message;
	int status;
	HWND newControl;
	wchar_t* resourceDll; 

	HANDLE mediumTextFont;
	HANDLE bigTextFont;
	RECT rect;

	resourceDll = AcquireFile(L"coapp.resources.dll", TRUE, NULL);
	if( resourceDll == NULL ) {
		TerminateApplicationWithError(IDS_UNABLE_TO_ACQUIRE_RESOURCES, L"Unable to find or download CoApp.Resources.dll");
		return 0;
	}

	if( LoadResources(resourceDll) == FALSE ) {
		TerminateApplicationWithError(IDS_UNABLE_TO_ACQUIRE_RESOURCES, L"Unable to load resources");
		return 0;
	}

	// get the desktop window size
	GetWindowRect(GetDesktopWindow(), &rect);

	// the rest of this is just to keep the user busy looking at an awesome dialog while the real work goes on.
	mediumTextFont =CreateFont (18, 0, 0, 0, FW_DONTCARE, FALSE, FALSE, FALSE, DEFAULT_CHARSET, OUT_TT_PRECIS, CLIP_DEFAULT_PRECIS, CLEARTYPE_QUALITY, DEFAULT_PITCH | FF_SWISS, L"Tahoma");
	bigTextFont =CreateFont (33, 0, 0, 0, FW_DONTCARE, FALSE, FALSE, FALSE, DEFAULT_CHARSET, OUT_TT_PRECIS, CLIP_DEFAULT_PRECIS, CLEARTYPE_QUALITY, DEFAULT_PITCH | FF_SWISS, L"Tahoma");

	// create the dialog, still hidden.
	StatusDialog = CreateDialog(resourceModule, MAKEINTRESOURCE(IDD_DIALOG1), NULL, DialogProc );
	
	// set the background bitmap to the same size as the window.
	SetWindowPos(GetDlgItem( StatusDialog, IDC_BACKGROUNDIMAGE), HWND_BOTTOM, 0,0,700 , 400, SWP_SHOWWINDOW);

	// set the image to the loaded bitmap
	SendMessage(GetDlgItem( StatusDialog, IDC_BACKGROUNDIMAGE), STM_SETIMAGE, (WPARAM)IMAGE_BITMAP,(LPARAM)background);

	// ensure that this window doesn't have a caption.
	SetWindowLongA( StatusDialog, GWL_STYLE, GetWindowLongA( StatusDialog, GWL_STYLE ) & ~WS_CAPTION );

	// create the logo bitmap too
	// bitmap = LoadBitmap(hInstance, MAKEINTRESOURCE(IDB_BITMAP_LOGO));
	logoControl = CreateWindowEx(0, L"STATIC", L"", WS_CHILD | SS_BITMAP | WS_VISIBLE, (680-111)/2, 255,111,111,StatusDialog, NULL, hInstance , NULL);
	SendMessage(logoControl , STM_SETIMAGE, (WPARAM)IMAGE_BITMAP,(LPARAM)logo);

	// move the progress bar into the righ tspot.
	SetWindowPos(GetDlgItem( StatusDialog, IDC_PROGRESS2), HWND_TOP, 65,200,550 , 30, SWP_SHOWWINDOW);

	// set progressbar to 0-288 (32(download) + 256(installer)) 
	SendMessage(GetDlgItem( StatusDialog, IDC_PROGRESS2), PBM_SETRANGE, 0, MAKELPARAM(0,288) );
	SetProgressValue( 1 );

	// Large Text String (on top of images)
	newControl = CreateWindowEx(0, L"STATIC", L"", WS_CHILD | WS_VISIBLE, 100,70,460,140,StatusDialog, (HMENU)IDC_STATICTEXT3, hInstance , NULL);

	// set Large Message Text Font
	SendMessage( newControl, WM_SETFONT, (WPARAM)bigTextFont ,TRUE);
	SetWindowText(newControl, GetString(IDS_MAIN_MESSAGE, L"It will be a few moments while CoApp configures the system components required to install the software."));

	newControl = CreateWindowEx(0, L"button", GetString(IDS_CANCEL, L"Cancel"), WS_CHILD | WS_VISIBLE | BS_OWNERDRAW, 650, 0,32,32,StatusDialog, (HMENU) IDC_X, hInstance , NULL);
	newControl = CreateWindowEx(0, L"button", GetString(IDS_CANCEL, L"Cancel"), WS_CHILD | WS_VISIBLE | BS_OWNERDRAW, 580, 340,82,32,StatusDialog, (HMENU) IDC_CANCEL, hInstance , NULL);
	SendMessage( newControl, WM_SETFONT, (WPARAM)mediumTextFont,TRUE);

	
	// Show the dialog window.
	SetWindowPos(StatusDialog, HWND_TOP, (rect.right - 680)/2,(rect.bottom- 380)/2,680,380, SWP_SHOWWINDOW);

	Ready = TRUE;

	// main thread message pump.
	while ((status = GetMessage(& message, 0, 0, 0)) != 0){
		if (status == -1)
			return -1;
		if (!IsDialogMessage (StatusDialog, & message)){
			TranslateMessage ( &message );
			DispatchMessage ( &message );
		}
	}

	return 0;
}

void* GetRegistryValue(const wchar_t* keyname, const wchar_t* valueName,DWORD expectedDataType  ) {
	LSTATUS status;
	HKEY key;
	int index=0;
	wchar_t* name = NewString();
	wchar_t** value = (wchar_t**)(void*)NewString();
	DWORD nameSize = BUFSIZE;
	DWORD valueSize = BUFSIZE;
	DWORD dataType;
	
	status = RegOpenKeyEx( HKEY_LOCAL_MACHINE, keyname, 0, KEY_READ | KEY_WOW64_64KEY , &key );

	if( status != ERROR_SUCCESS ) {
		goto release_value;
	}

	do {
		ZeroMemory( name, BUFSIZE);
		ZeroMemory( value, BUFSIZE);
		nameSize = BUFSIZE;
		valueSize = BUFSIZE;

		status = RegEnumValue(key, index, name, &nameSize, NULL, &dataType,(LPBYTE)value, &valueSize);
		if( !(status == ERROR_SUCCESS || status == ERROR_MORE_DATA) )
			goto release_value;
		
		if( lstrcmpi(valueName, name) == 0 ) {
			if( expectedDataType == REG_NONE || expectedDataType == dataType ) {
				goto release_name;
			} else {
				goto release_value;
			}
		}
		index++;
	}while( status == ERROR_SUCCESS || status == ERROR_MORE_DATA  );

release_value:  // called when the keys don't exist.
		free(value);
		value = NULL;

release_name:
		free(name);
		name = NULL;
		
	RegCloseKey(key);
	return value;
}

BOOL RegistryKeyPresent(const wchar_t* regkey) {
	wchar_t* keyname = DuplicateString( regkey );
	wchar_t* valuename = keyname;
	void* value; 

	while( *valuename != 0 && *valuename != L'#') {
		valuename++;
	}

	if( *valuename == L'#' ) {
		*valuename = 0;
		valuename++;
	}

	value = GetRegistryValue( keyname, valuename, REG_NONE );
	if( value ) {
		free(value);
		return TRUE;
	}

	DeleteString(&keyname);
	return FALSE;
}

void SetupMonitor() { 
	sectionHandle = CreateFileMapping(INVALID_HANDLE_VALUE, NULL, PAGE_READWRITE, 0, sizeof( struct MmioDataStructure), sectionName);
    eventHandle = CreateEvent(NULL, FALSE, FALSE, eventName);

	mmioData = (struct MmioDataStructure*)(MapViewOfFile(sectionHandle, FILE_MAP_WRITE, 0, 0, sizeof(struct MmioDataStructure)));

    // Common items for download and install
    wcscpy_s(mmioData->m_szEventName, MAX_PATH, eventName);

    // Download specific data
    mmioData->m_downloadFinished = FALSE;
    mmioData->m_downloadProgressSoFar = 0;
    mmioData->m_hrDownloadFinished = E_PENDING;
    mmioData->m_downloadAbort = FALSE;

    // Install specific data
    mmioData->m_installFinished = FALSE;
    mmioData->m_installProgressSoFar = 0;
    mmioData->m_hrInstallFinished = E_PENDING;
    mmioData->m_installAbort = FALSE;
    mmioData->m_hrInternalError = S_OK;

}

// Called by the chainer to start the chained setup - this blocks untils the setup is complete
HRESULT MonitorChainedInstaller( HANDLE process ) {
    HANDLE handles[2];
	int totalProgress = 0;
	HRESULT result;
	DWORD ret;
	int dir=1;

	handles[0] = process;
	handles[1] = eventHandle;

    while(!(mmioData->m_downloadFinished && mmioData->m_installFinished)) {
        ret= WaitForMultipleObjects(2, handles, FALSE, 500); // INFINITE ??
		switch(ret) {
        case WAIT_OBJECT_0: { // process handle closed.  Maybe it blew up, maybe it's just really fast.  Let's find out.
            if ((mmioData->m_downloadFinished && mmioData->m_installFinished) == FALSE) { 
				goto fin; // huh, not a good sign
            }
            break;
        }

		case WAIT_TIMEOUT:
        case WAIT_OBJECT_0 + 1:
			totalProgress = ((int)mmioData->m_downloadProgressSoFar/8) + (int)mmioData->m_installProgressSoFar; // (gives a number between 0-85%)
			if( totalProgress > 288 ) 
				totalProgress = 288;
			SetProgressValue( totalProgress );
			break;

		case WAIT_FAILED:
			break;

        default:
            break;
        }		
    }
fin:
    result = mmioData->m_hrInstallFinished;

	if (mmioData) {
        UnmapViewOfFile(mmioData);
    }

	mmioData = NULL;

	return result;
}
wchar_t* AcquireFile( const wchar_t* filename, BOOL searchOnline, const wchar_t* additionalDownloadServer );

int LaunchSecondStage() {
	wchar_t* commandLine = NULL;
	STARTUPINFO StartupInfo;
    PROCESS_INFORMATION ProcInfo;
	wchar_t* secondStage = AcquireFile(ManagedBootstrapFilename,TRUE,NULL);

	if( secondStage == NULL) {
		TerminateApplicationWithError(IDS_UNABLE_TO_FIND_SECOND_STAGE, L"Can't find second stage bootstrap.");
		return -1;
	}
	
	ZeroMemory(&StartupInfo, sizeof(STARTUPINFO) );
    StartupInfo.cb = sizeof( STARTUPINFO );

	commandLine = Sprintf(L"\"%s\" \"%s\"", secondStage, MsiFile);
	
	// launch the second-stage-bootstrapper.
	CreateProcess( secondStage, commandLine, NULL, NULL, TRUE, 0, NULL, NULL, &StartupInfo, &ProcInfo );

	DeleteString(&commandLine);
	DeleteString(&secondStage);

    ExitProcess(0);
    return 0;
}

void SetupMonitor();

unsigned __stdcall InstallNetFramework( void* pArguments ){
	STARTUPINFO StartupInfo;
    PROCESS_INFORMATION ProcInfo;
	wchar_t* commandLine = NULL;
	wchar_t* destinationFilename; 

	__try {
		if( IsShuttingDown )
			__leave;

		// before we go off downloading the .NET framework, 
		// let's see if it's already local somewhere.
		destinationFilename = AcquireFile(DotNetFullInstallerFilename, FALSE, NULL );
		if(!IsNullOrEmpty(destinationFilename) ) {
			__leave;
		}

		if( IsShuttingDown )
			__leave;

		destinationFilename = AcquireFile(DotNetWebInstallerFilename, FALSE, NULL );
		if(!IsNullOrEmpty(destinationFilename) ) {
			__leave;
		}

		if( IsShuttingDown )
			__leave;
		
		destinationFilename = AcquireFile(DotNetWebInstallerFilename, TRUE, DotNetWebInstallerUrl );
		if(!IsNullOrEmpty(destinationFilename) ) {
			__leave;
		}

		if( IsShuttingDown )
			__leave;

		destinationFilename = AcquireFile(DotNetFullInstallerFilename, TRUE, DotNetFullInstallerUrl );
		if(!IsNullOrEmpty(destinationFilename) ) {
			__leave;
		}
	} __finally {

	}

	if(IsNullOrEmpty(destinationFilename) ) {
		TerminateApplicationWithError(IDS_UNABLE_TO_DOWNLOAD_FRAMEWORK, L"Unable to download the .NET Framework 4.0 Installer (Required)");
		return 1;
	}

	__try {
		while(!Ready) // GUI has to be ready to proceed.
			Sleep(50);

		// (run install)
		ZeroMemory(&StartupInfo, sizeof(STARTUPINFO) );
		StartupInfo.cb = sizeof( STARTUPINFO );
		SetupMonitor();

		commandLine = Sprintf(L"\"%s\" /q /norestart /ChainingPackage coappbootstrapper /pipe coappbootstrapper", destinationFilename);
		// launch the second-stage-bootstrapper.
		CreateProcess( destinationFilename, commandLine, NULL, NULL, TRUE, 0, NULL, NULL, &StartupInfo, &ProcInfo );

		if( MonitorChainedInstaller(ProcInfo.hProcess) != S_OK ) {
			// hmm. bailed out of installing .NET
			if( IsShuttingDown ) {
				Shutdown();
			}
			TerminateApplicationWithError(IDS_FRAMEWORK_INSTALL_CANCELLED, L"The installation was abnormally cancelled.");
			__leave;
		}

		// after that's done
		if( IsShuttingDown )
			__leave;

		SetProgressValue( 288 );

		// check to see if .NET 4.0 is installed.
		if( RegistryKeyPresent(dot_net_regkey) ) {
			return LaunchSecondStage();
		}
		else {
			TerminateApplicationWithError(IDS_SOMETHING_ODD, L"Unknown Error.");
			return 1;
		}
	} __finally {
		ExitProcess(0);
		_endthreadex( 0 );
		WorkerThread = NULL;
	}
    
    return 0;
}

void ElevateSelf(const wchar_t* pszCmdLine) {
	SID_IDENTIFIER_AUTHORITY ntAuth = SECURITY_NT_AUTHORITY;
    PSID psid = NULL;
	BOOL isAdmin = FALSE;
	SHELLEXECUTEINFO sei;
	wchar_t modulePath[MAX_PATH];  
	wchar_t* newPath;
	int rc;

	__try {
		if( AllocateAndInitializeSid(&ntAuth,  2,  SECURITY_BUILTIN_DOMAIN_RID, DOMAIN_ALIAS_RID_ADMINS,  0, 0, 0, 0, 0, 0, &psid) && CheckTokenMembership(NULL, psid, &isAdmin ) && isAdmin ) {
			__leave; //Yep, we're an admin
		}

		ZeroMemory(&sei, sizeof(SHELLEXECUTEINFO) );
		GetModuleFileName(NULL, modulePath, MAX_PATH);
		// make sure path has a .EXE on the end.
		DebugPrintf(L"MODULE=%s",modulePath);
		

		newPath = TempFileName(Sprintf(L"%s.exe",GetFilenameFromPath(modulePath)));
		DebugPrintf(L"NEWPATH=%s",newPath);

		rc = CopyFile(modulePath, newPath, FALSE);
		DebugPrintf(L"copyfile: %d", rc );

		sei.lpFile = newPath;
		sei.lpVerb = L"runas";
		sei.lpParameters = pszCmdLine;
		sei.hwnd = GetForegroundWindow();
		sei.nShow = SW_NORMAL;
		sei.cbSize = sizeof(SHELLEXECUTEINFO);
		
		if (!ShellExecuteEx(&sei)) {
			rc = GetLastError();
			DebugPrintf(L"FAILURE: %d", rc );
			TerminateApplicationWithError(IDS_REQUIRES_ADMIN_RIGHTS,L"Administrator rights are required.");
			return;
		}
		ExitProcess(0);
	} __finally {
		FreeSid(psid);
	}
}

int WINAPI wWinMain( HINSTANCE hInstance, HINSTANCE hPrevInstance, wchar_t* pszCmdLine, int nCmdShow) {
	wchar_t *p;
    INITCOMMONCONTROLSEX iccs;
    ApplicationInstance = hInstance;

	// Elevate the process if it is not run as administrator.
	ElevateSelf(pszCmdLine);

	// get the path of this process
	BootstrapPath = NewString();
	GetModuleFileName(NULL, BootstrapPath, BUFSIZE);
	MsiFile = DuplicateString(pszCmdLine);
	
	BootstrapFolder = GetFolderFromPath(BootstrapPath);

	if( IsNullOrEmpty(MsiFile) ) {
		TerminateApplicationWithError(IDS_MISSING_MSI_FILE_ON_COMMANDLINE,L"Missing MSI filename on command line.");
		return 1;
	}

	if( *MsiFile == L'"' ) {
		// quoted command line. *sigh*.
		MsiFile++;
		p = MsiFile;
		while( *p != 0 && *p != L'"' ) {
			p++;
		}
		*p = 0; 
	} 

	MsiFolder = GetFolderFromPath(MsiFile);
	if( IsNullOrEmpty( MsiFolder ) ) {
		DeleteString(&MsiFolder);
		MsiFolder = NewString();
		GetCurrentDirectory(BUFSIZE, MsiFolder);
		MsiFile = UrlOrPathCombine(MsiFolder, MsiFile, '\\' );
	}

	// check to see if .NET 4.0 is installed.
	if( RegistryKeyPresent(dot_net_regkey) ) 
		return LaunchSecondStage();
	
	// load comctl32 v6, in particular the progress bar class
    iccs.dwSize = sizeof(INITCOMMONCONTROLSEX); // Naughty! :)
    iccs.dwICC  = ICC_PROGRESS_CLASS;
    InitCommonControlsEx(&iccs);
	BootstrapServerUrl = (wchar_t*)GetRegistryValue(L"Software\\CoApp", L"BootstrapServer",REG_SZ);

    // .NET 4.0 not there? install it.--- start worker thread
    WorkerThread = (HANDLE)_beginthreadex(NULL, 0, &InstallNetFramework, NULL, 0, &WorkerThreadId);
	
    // And, show the GUI
    return ShowGUI(hInstance);
}


typedef struct TDialogTemplate {
	DLGTEMPLATE dlgTemplate;
#pragma pack(2)
	WORD mNoMenu; // 0x0000 -- no menu
	WORD mStdClass; // 0x0000 -- standard dialog class
	wchar_t mTitle[5]; 
#pragma pack(4)
} DialogTemplate;
 

void TerminateApplicationWithError(int errorLevel, wchar_t* defaultText) {
	const wchar_t* message;
	wchar_t* help;

	MSG  msg;
	int status;
	HWND newControl;
	DWORD err;
	
	HANDLE mediumTextFont;
	HANDLE mediumUnderlinedTextFont;
	HANDLE bigTextFont;
	HANDLE giantTextFont;
	DialogTemplate dlg;
	RECT rect;

	wchar_t* resourceDll;

	// stop doing anything we were doing!
	Cancel();

	if( resourceModule == NULL ) { 
		// if the resourceModule isn't loaded, and can't be, it's not *super* critical... 
		resourceDll = AcquireFile(L"coapp.resources.dll", TRUE, NULL);
		if( resourceDll != NULL ) { 
			LoadResources(resourceDll);
		}
	}

	message = GetString(errorLevel, defaultText);
	help = Sprintf(L"%s%d",HelpUrl,errorLevel);
	ErrorLevel = errorLevel;

	if( StatusDialog != NULL ) {
		ShowWindow( StatusDialog, SW_HIDE);
	}

	//-----------------
	// Create Dialog
	//-----------------

    ZeroMemory(&dlg, sizeof(DialogTemplate) );
    dlg.dlgTemplate.style = WS_CAPTION | WS_VISIBLE | DS_CENTER;
	dlg.dlgTemplate.style= DS_SETFONT | DS_CENTER ;
	dlg.dlgTemplate.dwExtendedStyle =WS_EX_TOPMOST;
	dlg.dlgTemplate.cdit = 0;
	dlg.dlgTemplate.x = 0;
	dlg.dlgTemplate.y = 0;
	dlg.dlgTemplate.cx = 100;
	dlg.dlgTemplate.cy = 100;
	
	// get the desktop window size
	GetWindowRect(GetDesktopWindow(), &rect);

	// the rest of this is just to keep the user busy looking at an awesome dialog while the real work goes on.
	mediumTextFont =CreateFont (18, 0, 0, 0, FW_DONTCARE, FALSE, FALSE, FALSE, DEFAULT_CHARSET, OUT_TT_PRECIS, CLIP_DEFAULT_PRECIS, CLEARTYPE_QUALITY, DEFAULT_PITCH | FF_SWISS, L"Tahoma");
	mediumUnderlinedTextFont =CreateFont (18, 0, 0, 0, FW_DONTCARE, FALSE, TRUE, FALSE, DEFAULT_CHARSET, OUT_TT_PRECIS, CLIP_DEFAULT_PRECIS, CLEARTYPE_QUALITY, DEFAULT_PITCH | FF_SWISS, L"Tahoma");
	bigTextFont =CreateFont (33, 0, 0, 0, FW_DONTCARE, FALSE, FALSE, FALSE, DEFAULT_CHARSET, OUT_TT_PRECIS, CLIP_DEFAULT_PRECIS, CLEARTYPE_QUALITY, DEFAULT_PITCH | FF_SWISS, L"Tahoma");
	giantTextFont =CreateFont (200, 0, 0, 0, FW_DONTCARE, FALSE, FALSE, FALSE, DEFAULT_CHARSET, OUT_TT_PRECIS, CLIP_DEFAULT_PRECIS, CLEARTYPE_QUALITY, DEFAULT_PITCH | FF_SWISS, L"Tahoma");

	// create the dialog, still hidden.
	errorDialog = CreateDialogIndirect((HINSTANCE)ApplicationInstance, &dlg.dlgTemplate, NULL, DialogProc);
	err = GetLastError();

	// ensure that this window doesn't have a caption.
	SetWindowLongA( errorDialog, GWL_STYLE, GetWindowLongA( StatusDialog, GWL_STYLE ) & ~WS_CAPTION );

	// Large Text String (on top of images)
	newControl = CreateWindowEx(0, L"STATIC", L":(", WS_CHILD | WS_VISIBLE, 100,-15,460,250,errorDialog, (HMENU)(IDC_STATIC1+50), (HINSTANCE)ApplicationInstance , NULL);
	SendMessage( newControl, WM_SETFONT, (WPARAM)giantTextFont ,TRUE);
	

	newControl = CreateWindowEx(0, L"STATIC", GetString(IDS_CANT_CONTINUE, L"The installer has run into a problem that\r\ncouldn't be handled, and can't continue."), WS_CHILD | WS_VISIBLE, 100,200,560,90,errorDialog, (HMENU)(IDC_STATIC1+51), (HINSTANCE)ApplicationInstance , NULL);
	SendMessage( newControl, WM_SETFONT, (WPARAM)bigTextFont,TRUE);
	

	newControl = CreateWindowEx(0, L"STATIC", message , WS_CHILD | WS_VISIBLE, 100,290,560,22,errorDialog, (HMENU)(IDC_STATIC1+52), (HINSTANCE)ApplicationInstance , NULL);
	SendMessage( newControl, WM_SETFONT, (WPARAM)mediumTextFont,TRUE);
	

	newControl = CreateWindowEx(0, L"STATIC", GetString( IDS_FOR_ASSISTANCE, L"For assistance you can visit"), WS_CHILD | WS_VISIBLE | SS_RIGHT, 99,310,180,20,errorDialog, (HMENU)(IDC_STATIC1+54), (HINSTANCE)ApplicationInstance , NULL);
	SendMessage( newControl, WM_SETFONT, (WPARAM)mediumTextFont,TRUE);

	newControl = CreateWindowEx(0, L"STATIC", help, WS_CHILD | WS_VISIBLE | SS_NOTIFY, 283,310,360,20,errorDialog, (HMENU)(IDC_STATIC1+53), (HINSTANCE)ApplicationInstance , NULL);
	SendMessage( newControl, WM_SETFONT, (WPARAM)mediumUnderlinedTextFont,TRUE);

	newControl = CreateWindowEx(0, L"button", GetString(IDS_CANCEL, L"Cancel"), WS_CHILD | WS_VISIBLE | BS_OWNERDRAW, 650, 0,32,32,errorDialog, (HMENU) IDC_X, (HINSTANCE)ApplicationInstance , NULL);
	newControl = CreateWindowEx(0, L"button", L"Exit", WS_CHILD | WS_VISIBLE | BS_OWNERDRAW, 580, 340,82,32,errorDialog, (HMENU) IDC_CANCEL, (HINSTANCE)ApplicationInstance , NULL);
	SendMessage( newControl, WM_SETFONT, (WPARAM)mediumTextFont,TRUE);
 
	// Show the dialog window.
	SetWindowPos(errorDialog, HWND_TOP, (rect.right - 680)/2,(rect.bottom- 380)/2,680,380, SWP_SHOWWINDOW);

	Ready = TRUE;

	// main thread message pump.
	while ((status = GetMessage(& msg, 0, 0, 0)) != 0){
		if (status == -1)
			break;
		if (!IsDialogMessage (StatusDialog, &msg)){
			TranslateMessage ( &msg );
			DispatchMessage ( &msg );
		}
	}

	ExitProcess(errorLevel);
}