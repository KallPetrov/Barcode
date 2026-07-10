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

data class PickingOrderDto(
    val id: String,
    val name: String,
    val reference: String?,
    val strategy: String,
    val status: String,
    val assignedUserId: String?,
    val assignedUserName: String?,
    val startedByUserId: String?,
    val startedByUserName: String?,
    val completedByUserId: String?,
    val completedByUserName: String?,
    val startedAt: String?,
    val completedAt: String?,
    val notes: String?,
    val createdAt: String,
    val lines: List<PickingOrderLineDto>
)

data class PickingOrderLineDto(
    val id: String,
    val itemId: String,
    val itemName: String,
    val sourceLocationId: String?,
    val sourceLocationName: String?,
    val targetLocationId: String?,
    val targetLocationName: String?,
    val quantity: Double,
    val pickedQuantity: Double?,
    val notes: String?,
    val stockLines: List<PickingStockLineDto>
)

data class PickingStockLineDto(
    val id: String,
    val inventoryStockId: String,
    val itemName: String,
    val locationName: String,
    val quantity: Double,
    val batchNumber: String?,
    val serialNumber: String?,
    val expiryDate: String?,
    val pickedByUserId: String?,
    val pickedByUserName: String?,
    val pickedAt: String?
)

data class UpdatePickingLineRequest(
    val lineId: String,
    val pickedQuantity: Double,
    val overrideReason: String? = null,
    val temperatureCelsius: Double? = null
)

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

    @GET("api/picking")
    suspend fun getPickingOrders(): List<PickingOrderDto>

    @GET("api/picking/{id}")
    suspend fun getPickingOrder(@retrofit2.http.Path("id") id: String): PickingOrderDto

    @POST("api/picking/{id}/start")
    suspend fun startPickingOrder(@retrofit2.http.Path("id") id: String): PickingOrderDto

    @POST("api/picking/{id}/complete")
    suspend fun completePickingOrder(@retrofit2.http.Path("id") id: String): PickingOrderDto

    @POST("api/picking/{id}/stock-line")
    suspend fun updatePickingLine(
        @retrofit2.http.Path("id") id: String,
        @Body request: UpdatePickingLineRequest
    ): PickingOrderDto
}
