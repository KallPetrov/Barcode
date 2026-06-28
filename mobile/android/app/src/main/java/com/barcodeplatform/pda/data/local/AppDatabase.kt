package com.barcodeplatform.pda.data.local

import androidx.room.Dao
import androidx.room.Database
import androidx.room.Entity
import androidx.room.Insert
import androidx.room.OnConflictStrategy
import androidx.room.PrimaryKey
import androidx.room.Query
import androidx.room.Room
import androidx.room.RoomDatabase
import android.content.Context

@Entity(tableName = "sync_operations")
data class LocalSyncOperation(
    @PrimaryKey val clientOperationId: String,
    val operationType: String,
    val payloadJson: String,
    val createdAt: Long,
    val synced: Boolean = false
)

@Dao
interface SyncOperationDao {
    @Query("SELECT * FROM sync_operations WHERE synced = 0 ORDER BY createdAt ASC")
    suspend fun getPending(): List<LocalSyncOperation>

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun insert(operation: LocalSyncOperation)

    @Query("UPDATE sync_operations SET synced = 1 WHERE clientOperationId = :id")
    suspend fun markSynced(id: String)

    @Query("SELECT COUNT(*) FROM sync_operations WHERE synced = 0")
    suspend fun pendingCount(): Int
}

@Database(entities = [LocalSyncOperation::class], version = 1)
abstract class AppDatabase : RoomDatabase() {
    abstract fun syncOperationDao(): SyncOperationDao

    companion object {
        @Volatile private var instance: AppDatabase? = null

        fun getInstance(context: Context): AppDatabase =
            instance ?: synchronized(this) {
                instance ?: Room.databaseBuilder(
                    context.applicationContext,
                    AppDatabase::class.java,
                    "barcode_platform.db"
                ).build().also { instance = it }
            }
    }
}
