package com.barcodeplatform.pda

import android.app.Application
import com.barcodeplatform.pda.data.local.AppDatabase
import com.barcodeplatform.pda.data.repository.PlatformRepository

class CalacApp : Application() {
    lateinit var repository: PlatformRepository
        private set

    override fun onCreate() {
        super.onCreate()
        val db = AppDatabase.getInstance(this)
        repository = PlatformRepository(this, db.syncOperationDao())
    }
}
