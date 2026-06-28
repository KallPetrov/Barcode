package com.barcodeplatform.pda.data.api

import retrofit2.http.Body
import retrofit2.http.GET
import retrofit2.http.Header
import retrofit2.http.POST

data class LoginRequest(val username: String, val password: String)
data class LoginResponse(val token: String, val user: UserDto)
data class UserDto(
    val id: String,
    val username: String,
    val fullName: String,
    val role: String,
    val tenantId: String,
    val tenantName: String
)

data class RegisterDeviceRequest(
    val hardwareId: String,
    val name: String?,
    val manufacturer: String?,
    val model: String?,
    val osVersion: String?,
    val appVersion: String?,
    val batteryLevel: Int?
)

data class DeviceDto(val id: String, val hardwareId: String, val name: String)

data class SyncOperationItem(
    val clientOperationId: String,
    val operationType: String,
    val payloadJson: String
)

data class SyncPushRequest(val operations: List<SyncOperationItem>)
data class SyncPushResultItem(val clientOperationId: String, val success: Boolean, val errorMessage: String?)
data class SyncPushResponse(val results: List<SyncPushResultItem>)

interface ApiService {
    @POST("api/auth/login")
    suspend fun login(@Body request: LoginRequest): LoginResponse

    @POST("api/devices/register")
    suspend fun registerDevice(@Body request: RegisterDeviceRequest): DeviceDto

    @POST("api/devices/heartbeat")
    suspend fun heartbeat(@Body request: RegisterDeviceRequest): DeviceDto

    @POST("api/sync/push")
    suspend fun syncPush(
        @Header("X-Device-Id") deviceId: String,
        @Body request: SyncPushRequest
    ): SyncPushResponse

    @GET("api/sync/status")
    suspend fun syncStatus(@Header("X-Device-Id") deviceId: String): Map<String, Any>
}
