package com.barcodeplatform.pda.data.repository

import android.content.Context
import android.content.SharedPreferences
import android.os.Build
import android.provider.Settings
import com.barcodeplatform.pda.BuildConfig
import com.barcodeplatform.pda.data.api.ApiService
import com.barcodeplatform.pda.data.api.LoginRequest
import com.barcodeplatform.pda.data.api.RegisterDeviceRequest
import com.barcodeplatform.pda.data.api.SyncOperationItem
import com.barcodeplatform.pda.data.api.SyncPushRequest
import com.barcodeplatform.pda.data.local.LocalSyncOperation
import com.barcodeplatform.pda.data.local.SyncOperationDao
import okhttp3.Interceptor
import okhttp3.OkHttpClient
import okhttp3.logging.HttpLoggingInterceptor
import retrofit2.Retrofit
import retrofit2.converter.gson.GsonConverterFactory
import java.util.UUID

class PlatformRepository(
    private val context: Context,
    private val syncDao: SyncOperationDao
) {
    private val prefs: SharedPreferences =
        context.getSharedPreferences("barcode_platform", Context.MODE_PRIVATE)

    private var _api: ApiService? = null
    private val api: ApiService get() {
        if (_api == null) {
            val authInterceptor = Interceptor { chain ->
                val token = prefs.getString(KEY_TOKEN, null)
                val request = if (token != null) {
                    chain.request().newBuilder()
                        .addHeader("Authorization", "Bearer $token")
                        .build()
                } else chain.request()
                chain.proceed(request)
            }

            val client = OkHttpClient.Builder()
                .addInterceptor(authInterceptor)
                .addInterceptor(HttpLoggingInterceptor().apply {
                    level = HttpLoggingInterceptor.Level.BASIC
                })
                .build()

            val baseUrl = prefs.getString(KEY_API_URL, BuildConfig.API_BASE_URL) ?: BuildConfig.API_BASE_URL

            _api = Retrofit.Builder()
                .baseUrl(baseUrl)
                .client(client)
                .addConverterFactory(GsonConverterFactory.create())
                .build()
                .create(ApiService::class.java)
        }
        return _api!!
    }

    fun updateApiUrl(url: String) {
        val normalizedUrl = if (url.endsWith("/")) url else "$url/"
        prefs.edit().putString(KEY_API_URL, normalizedUrl).apply()
        _api = null // Force re-creation of API service
    }

    val apiUrl: String get() = prefs.getString(KEY_API_URL, BuildConfig.API_BASE_URL) ?: BuildConfig.API_BASE_URL

    val hardwareId: String
        get() = prefs.getString(KEY_HARDWARE_ID, null)
            ?: Settings.Secure.getString(context.contentResolver, Settings.Secure.ANDROID_ID)
                .also { prefs.edit().putString(KEY_HARDWARE_ID, it).apply() }

    val isLoggedIn: Boolean get() = prefs.getString(KEY_TOKEN, null) != null
    val userName: String? get() = prefs.getString(KEY_USER_NAME, null)

    suspend fun login(username: String, password: String): Result<Unit> = runCatching {
        val response = api.login(LoginRequest(username, password))
        prefs.edit()
            .putString(KEY_TOKEN, response.token)
            .putString(KEY_USER_NAME, response.user.fullName)
            .apply()
        registerDevice(context)
    }

    private suspend fun registerDevice(context: Context) {
        val battery = context.getSystemService(Context.BATTERY_SERVICE)
        val level = try {
            val bm = battery as android.os.BatteryManager
            bm.getIntProperty(android.os.BatteryManager.BATTERY_PROPERTY_CAPACITY)
        } catch (_: Exception) { null }

        api.registerDevice(
            RegisterDeviceRequest(
                hardwareId = hardwareId,
                name = "${Build.MANUFACTURER} ${Build.MODEL}",
                manufacturer = Build.MANUFACTURER,
                model = Build.MODEL,
                osVersion = Build.VERSION.RELEASE,
                appVersion = BuildConfig.VERSION_NAME,
                batteryLevel = level
            )
        )
    }

    suspend fun queueOperation(type: String, payloadJson: String) {
        syncDao.insert(
            LocalSyncOperation(
                clientOperationId = UUID.randomUUID().toString(),
                operationType = type,
                payloadJson = payloadJson,
                createdAt = System.currentTimeMillis()
            )
        )
    }

    suspend fun syncPending(): Result<Pair<Int, Int>> = runCatching {
        val pending = syncDao.getPending()
        if (pending.isEmpty()) return@runCatching Pair(0, 0)

        val response = api.syncPush(
            hardwareId,
            SyncPushRequest(pending.map {
                SyncOperationItem(it.clientOperationId, it.operationType, it.payloadJson)
            })
        )

        val successful = response.results.count { it.success }
        response.results.filter { it.success }.forEach {
            syncDao.markSynced(it.clientOperationId)
        }
        Pair(successful, syncDao.pendingCount())
    }

    suspend fun pendingCount(): Int = syncDao.pendingCount()

    fun logout() {
        prefs.edit().clear().apply()
    }

    companion object {
        private const val KEY_TOKEN = "token"
        private const val KEY_USER_NAME = "user_name"
        private const val KEY_HARDWARE_ID = "hardware_id"
        private const val KEY_API_URL = "api_url"
    }
}
