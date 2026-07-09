package com.barcodeplatform.pda

import android.app.Application
import com.barcodeplatform.pda.data.local.AppDatabase
import com.barcodeplatform.pda.data.repository.PlatformRepository
import com.barcodeplatform.pda.voice.VoiceService

class CalacApp : Application() {
    lateinit var repository: PlatformRepository
        private set
    lateinit var voiceService: VoiceService
        private set

    override fun onCreate() {
        super.onCreate()
        val db = AppDatabase.getInstance(this)
        repository = PlatformRepository(this, db.syncOperationDao())
        voiceService = VoiceService(this)
    }

    override fun onTerminate() {
        voiceService.shutdown()
        super.onTerminate()
    }
}
